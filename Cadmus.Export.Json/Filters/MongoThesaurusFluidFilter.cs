using Cadmus.Mongo;
using Fluid;
using Fluid.Values;
using Fusi.Tools.Configuration;
using MongoDB.Driver;
using Proteus.Core;
using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cadmus.Export.Json.Filters;

/// <summary>
/// A Fluid filter to render a MongoDB-based thesaurus. The input value is
/// assumed to be a string containing 0 or more thesaurus references in the
/// format specified by <see cref="MongoThesRendererFilterOptions.Pattern"/>.
/// For each reference found, the filter looks up the thesaurus and its entry
/// in the Mongo database specified by
/// <see cref="MongoThesRendererFilterOptions.ConnectionString"/>, and replaces
/// the reference with the entry's value if found, or leaves it unchanged if
/// not found. If the input contains no reference at all, it is returned
/// unchanged.
/// <para>Thesaurus IDs follow the same conventions used elsewhere in Cadmus:
/// a bare ID lacking a language suffix (e.g. <c>languages</c> rather than
/// <c>languages@en</c>) falls back to <c>@eng</c>, then <c>@en</c>, then the
/// bare ID itself; and aliases (thesauri with a non-null <c>TargetId</c>)
/// are followed to their target.</para>
/// <para>Thesauri fetched from the database are cached for the lifetime of
/// this filter instance (reset whenever it is reconfigured), so that
/// repeatedly rendering references to the same thesaurus (even with
/// different entry IDs) across many calls costs a single database round
/// trip.</para>
/// <para>Tag: <c>fluid-filter.mongo-thesaurus</c>.</para>
/// </summary>
[Tag("fluid-filter.mongo-thesaurus")]
public sealed class MongoThesaurusFluidFilter : MongoConsumerBase, IFluidFilter,
    IConfigurable<MongoThesRendererFilterOptions>
{
    private readonly ConcurrentDictionary<string, MongoThesaurus?> _cache = new();
    private MongoThesRendererFilterOptions? _options;
    private string? _databaseName;

    /// <summary>
    /// Configures the filter with the specified options.
    /// </summary>
    /// <param name="options">The options to configure the filter with.</param>
    /// <exception cref="ArgumentNullException">Options is null.</exception>
    public void Configure(MongoThesRendererFilterOptions options)
    {
        _options = options ??
            throw new ArgumentNullException(nameof(options));
        _databaseName = GetDatabaseName(_options.ConnectionString!);
        _cache.Clear();
    }

    /// <summary>
    /// Gets the thesaurus with the specified ID, either from the cache or,
    /// on a cache miss, by resolving it from the Mongo database (caching the
    /// result, even when the thesaurus is not found, to avoid repeating a
    /// failing lookup).
    /// </summary>
    private async Task<MongoThesaurus?> GetThesaurusAsync(string thesaurusId)
    {
        if (_cache.TryGetValue(thesaurusId, out MongoThesaurus? thesaurus))
            return thesaurus;

        thesaurus = await ResolveThesaurusAsync(thesaurusId);
        _cache[thesaurusId] = thesaurus;
        return thesaurus;
    }

    /// <summary>
    /// Resolves the thesaurus with the specified ID from the Mongo database,
    /// following the same conventions used elsewhere in Cadmus (see e.g.
    /// <c>MongoCadmusRepository.GetThesaurus</c>): (1) a bare ID lacking a
    /// language suffix (e.g. <c>languages</c> rather than <c>languages@en</c>)
    /// falls back to trying it with <c>@eng</c>, then <c>@en</c>, then as-is;
    /// (2) a resolved thesaurus that is an alias (i.e. has a non-null
    /// <c>TargetId</c>) is followed to its target, repeating the same
    /// resolution logic on the target ID.
    /// </summary>
    private async Task<MongoThesaurus?> ResolveThesaurusAsync(string id)
    {
        EnsureClientCreated(_options!.ConnectionString!);
        IMongoDatabase db = Client!.GetDatabase(_databaseName);
        IMongoCollection<MongoThesaurus> collection = db
            .GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);

        MongoThesaurus? thesaurus;
        do
        {
            thesaurus = await collection.Find(t => t.Id == id)
                .FirstOrDefaultAsync();

            if (thesaurus is null &&
                !id.EndsWith("@en", StringComparison.OrdinalIgnoreCase))
            {
                string bareId = Regex.Replace(id, "@[a-z]{2,3}$", "",
                    RegexOptions.IgnoreCase);

                foreach (string suffix in FallbackLanguageSuffixes)
                {
                    thesaurus = await collection
                        .Find(t => t.Id == bareId + suffix)
                        .FirstOrDefaultAsync();
                    if (thesaurus is not null) break;
                }
            }

            if (thesaurus?.TargetId is not null) id = thesaurus.TargetId;
        } while (thesaurus?.TargetId is not null);

        return thesaurus;
    }

    private static readonly string[] FallbackLanguageSuffixes =
        ["@eng", "@en", ""];

    /// <summary>
    /// Applies the filter to the specified input value.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="arguments">The filter arguments.</param>
    /// <param name="context">The template context.</param>
    /// <returns>The filtered value.</returns>
    /// <exception cref="InvalidOperationException">Connection string or
    /// pattern not set.</exception>
    public async ValueTask<FluidValue> Apply(FluidValue input,
        FilterArguments arguments, TemplateContext context)
    {
        // ensure options are set
        if (string.IsNullOrEmpty(_options?.ConnectionString))
            throw new InvalidOperationException(
                "MongoThesRendererFilterOptions.ConnectionString not set.");

        if (_options.PatternRegex is null)
            throw new InvalidOperationException(
                "MongoThesRendererFilterOptions.PatternRegex not set.");

        // if input isn't a string, just return it unchanged
        if (input is not StringValue s) return input;

        string text = s.ToStringValue();

        // find all the thesaurus references in the input; if none, there is
        // nothing to do and thus no need to hit the database at all
        MatchCollection matches = _options.PatternRegex.Matches(text);
        if (matches.Count == 0) return input;

        // prefetch (and cache) all the distinct thesauri referenced by the
        // input, so that each of them is fetched from the database at most
        // once even when referenced by several entries in the same input,
        // or across several calls to this filter
        foreach (string thesaurusId in matches
            .Select(m => m.Groups["t"].Value)
            .Where(id => id.Length > 0)
            .Distinct())
        {
            await GetThesaurusAsync(thesaurusId);
        }

        // rebuild the input, replacing each reference with its resolved
        // entry value when found, or leaving it unchanged otherwise
        StringBuilder sb = new(text.Length);
        int lastIndex = 0;

        foreach (Match m in matches)
        {
            sb.Append(text, lastIndex, m.Index - lastIndex);

            string thesaurusId = m.Groups["t"].Value;
            string entryId = m.Groups["e"].Value;

            string replacement = m.Value;
            if (_cache.TryGetValue(thesaurusId, out MongoThesaurus? thesaurus)
                && thesaurus is not null)
            {
                MongoThesaurusEntry? entry = thesaurus.Entries
                    .FirstOrDefault(e => e.Id == entryId);
                if (entry is not null) replacement = entry.Value;
            }

            sb.Append(replacement);
            lastIndex = m.Index + m.Length;
        }
        sb.Append(text, lastIndex, text.Length - lastIndex);

        return new StringValue(sb.ToString());
    }
}

/// <summary>
/// Options for <see cref="MongoThesaurusFluidFilter"/>.
/// </summary>
public class MongoThesRendererFilterOptions : DisabledOptions
{
    private string _pattern = @"\$\[(?<t>[^|]+)\|(?<e>[^]]+)\]";

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="MongoThesRendererFilterOptions"/> class, compiling
    /// <see cref="PatternRegex"/> from the default <see cref="Pattern"/> so
    /// that it is not <c>null</c> when the default pattern is used without
    /// ever setting <see cref="Pattern"/> explicitly.
    /// </summary>
    public MongoThesRendererFilterOptions()
    {
        PatternRegex = new Regex(_pattern, RegexOptions.Compiled,
            TimeSpan.FromSeconds(2));
    }

    /// <summary>
    /// Gets or sets the connection string to the Mongo database
    /// containing the thesauri.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the regular expression pattern representing a
    /// thesaurus ID to lookup: it is assumed that this expression has
    /// two named captures, <c>t</c> for the thesaurus ID, and <c>e</c>
    /// for its entry ID.
    /// </summary>
    public string Pattern
    {
        get => _pattern;
        set
        {
            if (_pattern != value)
            {
                _pattern = value;
                PatternRegex = string.IsNullOrEmpty(_pattern)
                    ? null
                    : new Regex(_pattern, RegexOptions.Compiled,
                        TimeSpan.FromSeconds(2));
            }
        }
    }

    /// <summary>
    /// Gets the compiled regular expression for <see cref="Pattern"/>.
    /// </summary>
    public Regex? PatternRegex { get; private set; }
}
