using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Proteus.Core.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Cadmus.Export.Filters;

/// <summary>
/// MongoDB based thesaurus renderer filter. This filter looks up
/// thesaurus IDs replacing them with their values when found. To this end,
/// it uses a regular expression pattern representing each thesaurus entry
/// ID with its thesaurus ID, and replaces it with the corresponding value
/// when found, otherwise with the raw entry ID.
/// <para>Tag: <c>it.vedph.text-filter.str.mongo-thesaurus</c>.
/// Old tag: <c>it.vedph.renderer-filter.mongo-thesaurus</c>.</para>
/// </summary>
[Tag("it.vedph.text-filter.str.mongo-thesaurus")]
public sealed class MongoThesTextFilter : TextFilter<string>,
    IConfigurable<MongoThesRendererFilterOptions>
{
    private readonly Dictionary<string, Thesaurus> _cache = [];
    private Regex? _idRegex;

    /// <summary>
    /// Configures this filter with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(MongoThesRendererFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _idRegex = new Regex(options.Pattern, RegexOptions.Compiled);
    }

    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="context">The optional renderer context.</param>
    /// <returns>The filtered text.</returns>
    protected override object? DoApply(string? text,
        IHasDataDictionary? context = null)
    {
        if (_idRegex == null || string.IsNullOrEmpty(text)) return text;

        ICadmusRepository? repository =
            (context as CadmusRendererContext)?.Repository;
        if (repository == null) return text;

        return _idRegex.Replace(text, (m) =>
        {
            string tId = m.Groups["t"].Value;
            string eId = m.Groups["e"].Value;
            Thesaurus? thesaurus = null;
            if (_cache.TryGetValue(tId, out Thesaurus? value))
            {
                thesaurus = value;
            }
            else
            {
                thesaurus = repository.GetThesaurus(tId);
                if (thesaurus != null) _cache[tId] = thesaurus;
            }

            return thesaurus != null
                ? thesaurus.Entries.FirstOrDefault(
                    e => e.Id == eId)?.Value ?? eId
                : eId;
        });
    }
}

/// <summary>
/// Options for <see cref="MongoThesTextFilter"/>.
/// </summary>
public class MongoThesRendererFilterOptions
{
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
    public string Pattern { get; set; }
        = @"\$(?<t>[@_a-zA-Z0-9]+):(?<e>[_a-zA-Z0-9]+)";
}
