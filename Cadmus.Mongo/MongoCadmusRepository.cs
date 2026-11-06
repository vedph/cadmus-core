using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Layers;
using Cadmus.Core.Storage;
using DiffMatchPatch;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Thesaurus = Cadmus.Core.Config.Thesaurus;

namespace Cadmus.Mongo;

/// <summary>
/// MongoDB based repository for Cadmus data.
/// Tag: <c>cadmus-repository.mongo</c>.
/// </summary>
/// <remarks>All the members are implemented asynchronously; each synchronous
/// member waits for its asynchronous counterpart, so that both share the
/// same code. This is safe in any context, as no continuation is resumed
/// on the caller's synchronization context.</remarks>
[Tag("cadmus-repository.mongo")]
public sealed class MongoCadmusRepository : MongoConsumerBase,
    ICadmusRepository,
    IConfigurable<MongoCadmusRepositoryOptions>
{
    private const string PARTS_PROPERTY = "parts";
    private const string SETTINGS_COLLECTION = "settings";

    private readonly IPartTypeProvider _partTypeProvider;
    private readonly IItemSortKeyBuilder _itemSortKeyBuilder;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly JsonSerializerOptions _exportJsonOptions;
    private readonly JsonWriterSettings _jsonSettings;
    private IEditOperationDiffAdapter<YXEditOperation>? _opDiffAdapter;
    private MongoCadmusRepositoryOptions? _options;
    private string? _databaseName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoCadmusRepository"/>
    /// class.
    /// </summary>
    /// <param name="partTypeProvider">The part type provider.</param>
    /// <param name="itemSortKeyBuilder">The item sort key builder.</param>
    /// <exception cref="ArgumentNullException">null options or part type
    /// provider</exception>
    public MongoCadmusRepository(
        IPartTypeProvider partTypeProvider,
        IItemSortKeyBuilder itemSortKeyBuilder)
    {
        _partTypeProvider = partTypeProvider ??
            throw new ArgumentNullException(nameof(partTypeProvider));
        _itemSortKeyBuilder = itemSortKeyBuilder ??
            throw new ArgumentNullException(nameof(itemSortKeyBuilder));

        _jsonSettings = new JsonWriterSettings
        {
            Indent = false
        };
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        // exported documents are meant to be human-readable and editable
        _exportJsonOptions = new JsonSerializerOptions(_jsonOptions)
        {
            WriteIndented = true,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(MongoCadmusRepositoryOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _databaseName = GetDatabaseName(_options.ConnectionString!);
    }

    #region Helpers
    private IMongoDatabase GetDatabase()
    {
        if (_options?.ConnectionString == null)
        {
            throw new InvalidOperationException(
                $"{nameof(MongoCadmusRepository)} not configured");
        }
        EnsureClientCreated(_options.ConnectionString);
        return Client!.GetDatabase(_databaseName);
    }

    private IMongoCollection<T> GetCollection<T>(string name) =>
        GetDatabase().GetCollection<T>(name);

    // the synchronous members wait for the asynchronous ones: this is safe
    // as all the awaits in this class use ConfigureAwait(false)
    private static T Wait<T>(Task<T> task) => task.GetAwaiter().GetResult();

    private static void Wait(Task task) => task.GetAwaiter().GetResult();

    /// <summary>
    /// Builds a case-sensitive or insensitive regular expression matching
    /// any text containing <paramref name="value"/>, which is a literal.
    /// </summary>
    private static BsonRegularExpression BuildContainsRegex(string value,
        bool ignoreCase) =>
        new(Regex.Escape(value), ignoreCase ? "i" : "");

    /// <summary>
    /// Skips and limits the specified find according to the paging options.
    /// A page size of 0 means no limit.
    /// </summary>
    private static IFindFluent<T, T> ApplyPaging<T>(IFindFluent<T, T> find,
        PagingOptions options)
    {
        find = find.Skip(options.GetSkipCount());
        return options.PageSize > 0 ? find.Limit(options.PageSize) : find;
    }

    /// <summary>
    /// Resolves the specified property name of <typeparamref name="T"/>
    /// (case insensitive) into its document field name, as used for sorting.
    /// The name can be followed by a dot and a path into a subdocument
    /// (e.g. <c>Content.y</c> for parts), which is used as it is.
    /// </summary>
    /// <exception cref="ArgumentException">unknown property</exception>
    private static string ResolveFieldPath<T>(string property)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(property);

        int i = property.IndexOf('.');
        string name = i > -1 ? property[..i] : property;

        BsonMemberMap? map = BsonClassMap.LookupClassMap(typeof(T))
            .AllMemberMaps.FirstOrDefault(m => string.Equals(
                m.MemberName, name, StringComparison.OrdinalIgnoreCase));
        if (map == null)
        {
            throw new ArgumentException(
                $"Unknown sort field for {typeof(T).Name}: {property}",
                nameof(property));
        }
        return i > -1 ? map.ElementName + property[i..] : map.ElementName;
    }

    /// <summary>
    /// Builds the sort definition from the specified custom sort expressions.
    /// The document ID is always appended (unless already present) to grant
    /// a stable order for paging.
    /// </summary>
    private static SortDefinition<T> BuildSort<T>(
        IList<Tuple<string, bool>> expressions)
    {
        List<SortDefinition<T>> sorts = [];
        HashSet<string> paths = [];
        foreach (Tuple<string, bool> t in expressions)
        {
            string path = ResolveFieldPath<T>(t.Item1);
            if (!paths.Add(path)) continue;
            sorts.Add(t.Item2
                ? Builders<T>.Sort.Ascending(path)
                : Builders<T>.Sort.Descending(path));
        }
        if (!paths.Contains("_id")) sorts.Add(Builders<T>.Sort.Ascending("_id"));
        return Builders<T>.Sort.Combine(sorts);
    }

    /// <summary>
    /// Serializes the specified UTC date and time in the same format used
    /// in part contents.
    /// </summary>
    private string SerializeDateTime(DateTime dt) =>
        JsonSerializer.Serialize(dt, _jsonOptions).Trim('"');
    #endregion

    #region Flags
    /// <summary>
    /// Gets the flag definitions.
    /// </summary>
    /// <returns>definitions</returns>
    public IList<FlagDefinition> GetFlagDefinitions() =>
        Wait(GetFlagDefinitionsAsync());

    /// <summary>
    /// Gets the flag definitions.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Definitions, sorted by ID.</returns>
    public async Task<IList<FlagDefinition>> GetFlagDefinitionsAsync(
        CancellationToken cancel = default)
    {
        var flags = GetCollection<MongoFlagDefinition>(
            MongoFlagDefinition.COLLECTION);

        List<MongoFlagDefinition> defs = await flags
            .Find(FilterDefinition<MongoFlagDefinition>.Empty)
            .SortBy(f => f.Id)
            .ToListAsync(cancel).ConfigureAwait(false);
        return defs.ConvertAll(d => d.ToFlagDefinition());
    }

    /// <summary>
    /// Gets the specified flag definition.
    /// </summary>
    /// <param name="id">The flag identifier.</param>
    /// <returns>definition or null if not found</returns>
    public FlagDefinition? GetFlagDefinition(int id) =>
        Wait(GetFlagDefinitionAsync(id));

    /// <summary>
    /// Gets the specified flag definition.
    /// </summary>
    /// <param name="id">The flag identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Definition or null if not found.</returns>
    public async Task<FlagDefinition?> GetFlagDefinitionAsync(int id,
        CancellationToken cancel = default)
    {
        var flags = GetCollection<MongoFlagDefinition>(
            MongoFlagDefinition.COLLECTION);

        MongoFlagDefinition? def = await flags.Find(f => f.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        return def?.ToFlagDefinition();
    }

    /// <summary>
    /// Adds or updates the specified flag definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <exception cref="ArgumentNullException">null definition</exception>
    public void AddFlagDefinition(FlagDefinition definition) =>
        Wait(AddFlagDefinitionAsync(definition));

    /// <summary>
    /// Adds or updates the specified flag definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null definition</exception>
    public async Task AddFlagDefinitionAsync(FlagDefinition definition,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(definition);

        var flags = GetCollection<MongoFlagDefinition>(
            MongoFlagDefinition.COLLECTION);

        await flags.ReplaceOneAsync(f => f.Id == definition.Id,
            new MongoFlagDefinition(definition),
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified flag definition.
    /// </summary>
    /// <param name="id">The flag identifier.</param>
    public void DeleteFlagDefinition(int id) =>
        Wait(DeleteFlagDefinitionAsync(id));

    /// <summary>
    /// Deletes the specified flag definition.
    /// </summary>
    /// <param name="id">The flag identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    public async Task DeleteFlagDefinitionAsync(int id,
        CancellationToken cancel = default)
    {
        var flags = GetCollection<MongoFlagDefinition>(
            MongoFlagDefinition.COLLECTION);

        await flags.DeleteOneAsync(f => f.Id == id, cancel)
            .ConfigureAwait(false);
    }
    #endregion

    #region Facets
    /// <summary>
    /// Gets the item's facets.
    /// </summary>
    /// <returns>facets</returns>
    public IList<FacetDefinition> GetFacetDefinitions() =>
        Wait(GetFacetDefinitionsAsync());

    /// <summary>
    /// Gets the item's facets.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Facets, sorted by label.</returns>
    public async Task<IList<FacetDefinition>> GetFacetDefinitionsAsync(
        CancellationToken cancel = default)
    {
        var facets = GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION);

        List<MongoFacetDefinition> defs = await facets
            .Find(FilterDefinition<MongoFacetDefinition>.Empty)
            .SortBy(f => f.Label)
            .ToListAsync(cancel).ConfigureAwait(false);
        return defs.ConvertAll(f => f.ToFacetDefinition());
    }

    /// <summary>
    /// Gets the specified item's facet.
    /// </summary>
    /// <param name="id">The facet identifier.</param>
    /// <returns>facet or null if not found</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public FacetDefinition? GetFacetDefinition(string id) =>
        Wait(GetFacetDefinitionAsync(id));

    /// <summary>
    /// Gets the specified item's facet.
    /// </summary>
    /// <param name="id">The facet identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Facet or null if not found.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task<FacetDefinition?> GetFacetDefinitionAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var facets = GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION);

        MongoFacetDefinition? facet = await facets.Find(f => f.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        return facet?.ToFacetDefinition();
    }

    /// <summary>
    /// Adds or updates the specified facet.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <exception cref="ArgumentNullException">null facet</exception>
    public void AddFacetDefinition(FacetDefinition facet) =>
        Wait(AddFacetDefinitionAsync(facet));

    /// <summary>
    /// Adds or updates the specified facet.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null facet</exception>
    public async Task AddFacetDefinitionAsync(FacetDefinition facet,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(facet);

        var facets = GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION);

        await facets.ReplaceOneAsync(f => f.Id == facet.Id,
            new MongoFacetDefinition(facet),
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified facet.
    /// </summary>
    /// <param name="id">The facet identifier.</param>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public void DeleteFacetDefinition(string id) =>
        Wait(DeleteFacetDefinitionAsync(id));

    /// <summary>
    /// Deletes the specified facet.
    /// </summary>
    /// <param name="id">The facet identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task DeleteFacetDefinitionAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var facets = GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION);

        await facets.DeleteOneAsync(f => f.Id == id, cancel)
            .ConfigureAwait(false);
    }
    #endregion

    #region Thesauri
    private static FilterDefinition<MongoThesaurus> BuildThesaurusFilter(
        ThesaurusFilter? filter)
    {
        FilterDefinitionBuilder<MongoThesaurus> builder =
            Builders<MongoThesaurus>.Filter;
        FilterDefinition<MongoThesaurus> f = builder.Empty;
        if (filter == null) return f;

        // ID: contains
        if (!string.IsNullOrEmpty(filter.Id))
            f &= builder.Regex(t => t.Id, BuildContainsRegex(filter.Id, false));

        if (filter.IsAlias != null)
        {
            f &= filter.IsAlias.Value
                ? builder.Ne(t => t.TargetId, null)
                : builder.Eq(t => t.TargetId, null);
        }

        // language: ID ends with @ + language
        if (!string.IsNullOrEmpty(filter.Language))
        {
            f &= builder.Regex(t => t.Id, new BsonRegularExpression(
                Regex.Escape("@" + filter.Language) + "$"));
        }

        return f;
    }

    /// <summary>
    /// Gets the IDs of all the thesauri, or of all those matching the
    /// specified filter.
    /// </summary>
    /// <param name="filter">The optional filter.</param>
    /// <returns>IDs</returns>
    public IList<string> GetThesaurusIds(ThesaurusFilter? filter = null) =>
        Wait(GetThesaurusIdsAsync(filter));

    /// <summary>
    /// Gets the IDs of all the thesauri, or of all those matching the
    /// specified filter.
    /// </summary>
    /// <param name="filter">The optional filter. When specified, its paging
    /// options are applied; a page size of 0 means all the matching IDs.
    /// </param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>IDs, sorted.</returns>
    public async Task<IList<string>> GetThesaurusIdsAsync(
        ThesaurusFilter? filter = null, CancellationToken cancel = default)
    {
        var thesauri = GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);

        IFindFluent<MongoThesaurus, MongoThesaurus> find = thesauri
            .Find(BuildThesaurusFilter(filter))
            .SortBy(t => t.Id);
        if (filter != null) find = ApplyPaging(find, filter);

        return await find.Project(t => t.Id)
            .ToListAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the specified page of thesauri.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<Thesaurus> GetThesauri(ThesaurusFilter filter) =>
        Wait(GetThesauriAsync(filter));

    /// <summary>
    /// Gets the specified page of thesauri.
    /// </summary>
    /// <param name="filter">The filter. A page size of 0 means all the
    /// matching thesauri.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public async Task<DataPage<Thesaurus>> GetThesauriAsync(
        ThesaurusFilter filter, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var thesauri = GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);
        FilterDefinition<MongoThesaurus> f = BuildThesaurusFilter(filter);

        int total = (int)await thesauri.CountDocumentsAsync(f,
            cancellationToken: cancel).ConfigureAwait(false);
        if (total == 0)
        {
            return new DataPage<Thesaurus>(filter.PageNumber, filter.PageSize,
                0, Array.Empty<Thesaurus>());
        }

        List<MongoThesaurus> page = await ApplyPaging(
            thesauri.Find(f).SortBy(t => t.Id), filter)
            .ToListAsync(cancel).ConfigureAwait(false);

        return new DataPage<Thesaurus>(filter.PageNumber, filter.PageSize,
            total, page.ConvertAll(t => t.ToThesaurus()));
    }

    /// <summary>
    /// Gets the list of IDs to look for when getting the thesaurus with
    /// the specified ID, in their order of preference: the ID itself, and
    /// then the same ID with <c>@eng</c>, <c>@en</c>, or no language suffix.
    /// </summary>
    private static List<string> GetThesaurusIdCandidates(string id)
    {
        List<string> ids = [id];
        string bareId = Regex.Replace(id, @"\@[a-z]{2,3}$", "");
        foreach (string suffix in new[] { "@eng", "@en", "" })
        {
            string candidate = bareId + suffix;
            if (!ids.Contains(candidate)) ids.Add(candidate);
        }
        return ids;
    }

    /// <summary>
    /// Gets the thesaurus with the specified ID.
    /// </summary>
    /// <param name="id">The thesaurus ID. This usually should include the
    /// language code suffix. When this language is not found, the repository
    /// tries to fall back to these languages (in this order): <c>eng</c>,
    /// <c>en</c>, or no language at all. If there is still no match, null
    /// will be returned.</param>
    /// <returns>Thesaurus, or null if not found. Notice that when the
    /// requested thesaurus is an alias, it is its target thesaurus that
    /// will be returned, if any.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public Thesaurus? GetThesaurus(string id) => Wait(GetThesaurusAsync(id));

    /// <summary>
    /// Gets the thesaurus with the specified ID.
    /// </summary>
    /// <param name="id">The thesaurus ID. This usually should include the
    /// language code suffix. When this language is not found, the repository
    /// tries to fall back to these languages (in this order): <c>eng</c>,
    /// <c>en</c>, or no language at all. If there is still no match, null
    /// will be returned.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Thesaurus, or null if not found. Notice that when the
    /// requested thesaurus is an alias, it is its target thesaurus that
    /// will be returned, if any (aliases are followed with the same
    /// language fallback; a cycle of aliases returns null).</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task<Thesaurus?> GetThesaurusAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var thesauri = GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);
        HashSet<string> visited = new(StringComparer.Ordinal);
        string currentId = id;

        while (visited.Add(currentId))
        {
            // get all the candidates at once, and pick the preferred one
            List<string> candidates = GetThesaurusIdCandidates(currentId);
            List<MongoThesaurus> found = await thesauri
                .Find(Builders<MongoThesaurus>.Filter.In(t => t.Id, candidates))
                .ToListAsync(cancel).ConfigureAwait(false);

            MongoThesaurus? thesaurus = candidates
                .Select(c => found.Find(t => t.Id == c))
                .FirstOrDefault(t => t != null);
            if (thesaurus == null) return null;

            // if this is an alias, repeat the search for its target
            if (thesaurus.TargetId == null) return thesaurus.ToThesaurus();
            currentId = thesaurus.TargetId;
        }

        // cycle of aliases
        return null;
    }

    /// <summary>
    /// Adds or updates the specified thesaurus.
    /// </summary>
    /// <param name="thesaurus">The thesaurus.</param>
    /// <exception cref="ArgumentNullException">null thesaurus</exception>
    public void AddThesaurus(Thesaurus thesaurus) =>
        Wait(AddThesaurusAsync(thesaurus));

    /// <summary>
    /// Adds or updates the specified thesaurus.
    /// </summary>
    /// <param name="thesaurus">The thesaurus.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null thesaurus</exception>
    public async Task AddThesaurusAsync(Thesaurus thesaurus,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(thesaurus);

        var thesauri = GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);

        await thesauri.ReplaceOneAsync(t => t.Id == thesaurus.Id,
            new MongoThesaurus(thesaurus),
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified thesaurus.
    /// </summary>
    /// <param name="id">The thesaurus ID.</param>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public void DeleteThesaurus(string id) => Wait(DeleteThesaurusAsync(id));

    /// <summary>
    /// Deletes the specified thesaurus.
    /// </summary>
    /// <param name="id">The thesaurus ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task DeleteThesaurusAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var thesauri = GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);

        await thesauri.DeleteOneAsync(t => t.Id == id, cancel)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all the thesauri IDs which are aliases directly pointing to the
    /// thesaurus with the specified ID.
    /// </summary>
    /// <param name="targetId">The target thesaurus identifier.</param>
    /// <returns>List of alias IDs in alphabetical order.</returns>
    /// <exception cref="ArgumentNullException">null target ID</exception>
    public IList<string> GetThesaurusAliases(string targetId) =>
        Wait(GetThesaurusAliasesAsync(targetId));

    /// <summary>
    /// Gets all the thesauri IDs which are aliases directly pointing to the
    /// thesaurus with the specified ID.
    /// </summary>
    /// <param name="targetId">The target thesaurus identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>List of alias IDs in alphabetical order.</returns>
    /// <exception cref="ArgumentNullException">null target ID</exception>
    public async Task<IList<string>> GetThesaurusAliasesAsync(string targetId,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(targetId);

        var thesauri = GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION);

        return await thesauri.Find(t => t.TargetId == targetId)
            .SortBy(t => t.Id)
            .Project(t => t.Id)
            .ToListAsync(cancel).ConfigureAwait(false);
    }
    #endregion

    #region Items
    private static FilterDefinition<T> BuildFlagsFilter<T>(
        FlagMatching matching, int flags)
        where T : IHasFlags
    {
        FilterDefinitionBuilder<T> builder = Builders<T>.Filter;
        return matching switch
        {
            FlagMatching.BitsAnySet => builder.BitsAnySet(i => i.Flags, flags),
            FlagMatching.BitsAllClear => builder.BitsAllClear(i => i.Flags, flags),
            FlagMatching.BitsAnyClear => builder.BitsAnyClear(i => i.Flags, flags),
            _ => builder.BitsAllSet(i => i.Flags, flags),
        };
    }

    private static FilterDefinition<MongoItem> BuildItemFilter(ItemFilter filter)
    {
        FilterDefinitionBuilder<MongoItem> builder = Builders<MongoItem>.Filter;
        FilterDefinition<MongoItem> f = builder.Empty;

        // title and description: contains, case insensitive
        if (!string.IsNullOrEmpty(filter.Title))
        {
            f &= builder.Regex(i => i.Title,
                BuildContainsRegex(filter.Title, true));
        }

        if (!string.IsNullOrEmpty(filter.Description))
        {
            f &= builder.Regex(i => i.Description,
                BuildContainsRegex(filter.Description, true));
        }

        if (!string.IsNullOrEmpty(filter.FacetId))
            f &= builder.Eq(i => i.FacetId, filter.FacetId);

        // group: contains, case sensitive
        if (!string.IsNullOrEmpty(filter.GroupId))
        {
            f &= builder.Regex(i => i.GroupId,
                BuildContainsRegex(filter.GroupId, false));
        }

        if (filter.Flags.HasValue)
        {
            f &= BuildFlagsFilter<MongoItem>(filter.FlagMatching,
                filter.Flags.Value);
        }

        if (!string.IsNullOrEmpty(filter.UserId))
            f &= builder.Eq(i => i.UserId, filter.UserId);

        if (filter.MinModified.HasValue)
            f &= builder.Gte(i => i.TimeModified, filter.MinModified.Value);

        if (filter.MaxModified.HasValue)
            f &= builder.Lte(i => i.TimeModified, filter.MaxModified.Value);

        return f;
    }

    /// <summary>
    /// Gets a page of items.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>items page</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    public DataPage<ItemInfo> GetItems(ItemFilter filter) =>
        Wait(GetItemsAsync(filter));

    /// <summary>
    /// Gets a page of items, sorted by their sort key and ID. Title and
    /// description are matched when they contain the filter's value (case
    /// insensitive), the group ID when it contains the filter's value (case
    /// sensitive); facet and user ID must be equal.
    /// </summary>
    /// <param name="filter">The filter. A page size of 0 means all the
    /// matching items.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Items page.</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    public async Task<DataPage<ItemInfo>> GetItemsAsync(ItemFilter filter,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var items = GetCollection<MongoItem>(MongoItem.COLLECTION);
        FilterDefinition<MongoItem> f = BuildItemFilter(filter);

        int total = (int)await items.CountDocumentsAsync(f,
            cancellationToken: cancel).ConfigureAwait(false);
        if (total == 0)
        {
            return new DataPage<ItemInfo>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<ItemInfo>());
        }

        List<MongoItem> page = await ApplyPaging(items.Find(f)
            .SortBy(i => i.SortKey)
            .ThenBy(i => i.Id), filter)
            .ToListAsync(cancel).ConfigureAwait(false);

        return new DataPage<ItemInfo>(
            filter.PageNumber, filter.PageSize, total,
            page.ConvertAll(i => i.ToItemInfo()));
    }

    /// <summary>
    /// Gets the specified item.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="includeParts">if set to <c>true</c>, include all the
    /// item's parts.</param>
    /// <returns>item or null if not found</returns>
    /// <exception cref="ArgumentNullException">null item ID</exception>
    /// <exception cref="InvalidOperationException">unable to instantiate part
    /// </exception>
    public IItem? GetItem(string id, bool includeParts = true) =>
        Wait(GetItemAsync(id, includeParts));

    /// <summary>
    /// Gets the specified item.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="includeParts">if set to <c>true</c>, include all the
    /// item's parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Item or null if not found.</returns>
    /// <exception cref="ArgumentNullException">null item ID</exception>
    /// <exception cref="InvalidOperationException">unable to instantiate part
    /// </exception>
    public async Task<IItem?> GetItemAsync(string id, bool includeParts = true,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        IMongoDatabase db = GetDatabase();
        var items = db.GetCollection<MongoItem>(MongoItem.COLLECTION);

        MongoItem? mongoItem = await items.Find(i => i.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (mongoItem == null) return null;

        Item item = mongoItem.ToItem();

        // add parts if required
        if (includeParts)
        {
            List<MongoPart> parts = await db.GetCollection<MongoPart>(
                MongoPart.COLLECTION)
                .Find(p => p.ItemId == id)
                .ToListAsync(cancel).ConfigureAwait(false);
            item.Parts.AddRange(InstantiateParts(parts));
        }

        return item;
    }

    /// <summary>
    /// Adds or updates the specified item.
    /// </summary>
    /// <remarks>Note that the item's parts are ignored. If you want to add
    /// parts, use the <see cref="AddPart"/> method.</remarks>
    /// <param name="item">The item.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <exception cref="ArgumentNullException">null item</exception>
    public void AddItem(IItem item, bool history = true) =>
        Wait(AddItemAsync(item, history));

    /// <summary>
    /// Adds or updates the specified item. Its sort key is rebuilt, and
    /// its modified time set to now. If the item already exists, its
    /// creator and creation time are kept.
    /// </summary>
    /// <remarks>Note that the item's parts are ignored. If you want to add
    /// parts, use the <see cref="AddPartAsync"/> method.</remarks>
    /// <param name="item">The item.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null item</exception>
    public async Task AddItemAsync(IItem item, bool history = true,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        IMongoDatabase db = GetDatabase();

        // update sort key
        item.SortKey = _itemSortKeyBuilder.BuildKey(item, this);
        // set time modified
        item.TimeModified = DateTime.UtcNow;

        var items = db.GetCollection<MongoItem>(MongoItem.COLLECTION);

        // creator and creation time cannot be changed once set
        MongoItem? old = await items.Find(i => i.Id == item.Id)
            .Project<MongoItem>(Builders<MongoItem>.Projection
                .Include(i => i.CreatorId)
                .Include(i => i.TimeCreated))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (old?.CreatorId != null)
        {
            item.CreatorId = old.CreatorId;
            item.TimeCreated = old.TimeCreated;
        }

        if (history)
        {
            // add the new item to the history, as newly created or updated
            MongoHistoryItem historyItem = new(item)
            {
                Status = old != null ? EditStatus.Updated : EditStatus.Created
            };

            await db.GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION)
                .InsertOneAsync(historyItem, cancellationToken: cancel)
                .ConfigureAwait(false);
        }

        await items.ReplaceOneAsync(i => i.Id == item.Id,
            new MongoItem(item),
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <remarks>Note that deleting an item also deletes all its parts.
    /// </remarks>
    /// <param name="id">The identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <exception cref="ArgumentNullException">null ID or user ID</exception>
    public void DeleteItem(string id, string userId, bool history = true) =>
        Wait(DeleteItemAsync(id, userId, history));

    /// <summary>
    /// Deletes the item with all its parts.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID or user ID</exception>
    public async Task DeleteItemAsync(string id, string userId,
        bool history = true, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        IMongoDatabase db = GetDatabase();

        // find the item to delete
        var items = db.GetCollection<MongoItem>(MongoItem.COLLECTION);
        MongoItem? mongoItem = await items.Find(i => i.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (mongoItem == null) return;

        DateTime now = DateTime.UtcNow;

        // delete the item's parts, storing them into history as deleted
        // now by the specified user
        var partsCollection = db.GetCollection<MongoPart>(MongoPart.COLLECTION);
        List<MongoPart> parts = await partsCollection
            .Find(p => p.ItemId == id)
            .ToListAsync(cancel).ConfigureAwait(false);
        if (parts.Count > 0)
        {
            if (history)
            {
                await db.GetCollection<MongoHistoryPart>(
                    MongoHistoryPart.COLLECTION)
                    .InsertManyAsync(parts.Select(p => new MongoHistoryPart(p)
                    {
                        UserId = userId,
                        Status = EditStatus.Deleted,
                        TimeModified = now
                    }), cancellationToken: cancel).ConfigureAwait(false);
            }

            // delete just the parts we stored into history
            await partsCollection.DeleteManyAsync(
                Builders<MongoPart>.Filter.In(p => p.Id,
                    parts.Select(p => p.Id)),
                cancel).ConfigureAwait(false);
        }

        // store the item being deleted into history, as deleted now by
        // the specified user
        if (history)
        {
            MongoHistoryItem historyItem = new(mongoItem)
            {
                UserId = userId,
                Status = EditStatus.Deleted,
                TimeModified = now
            };

            await db.GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION)
                .InsertOneAsync(historyItem, cancellationToken: cancel)
                .ConfigureAwait(false);
        }

        // delete the item
        await items.DeleteOneAsync(i => i.Id == id, cancel)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Set the flags of the item(s) with the specified ID(s).
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The item identifier(s).</param>
    /// <param name="flags">The flags value.</param>
    /// <exception cref="ArgumentNullException">null ID(s)</exception>
    public void SetItemFlags(IList<string> ids, int flags) =>
        Wait(SetItemFlagsAsync(ids, flags));

    /// <summary>
    /// Set the flags of the item(s) with the specified ID(s).
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The item identifier(s).</param>
    /// <param name="flags">The flags value.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID(s)</exception>
    public async Task SetItemFlagsAsync(IList<string> ids, int flags,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = GetCollection<MongoItem>(MongoItem.COLLECTION);
        await items.UpdateManyAsync(
            Builders<MongoItem>.Filter.In(i => i.Id, ids),
            Builders<MongoItem>.Update.Set(i => i.Flags, flags),
            cancellationToken: cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Set the group ID of the item(s) with the specified ID(s).
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The items identifiers.</param>
    /// <param name="groupId">The group ID (can be null).</param>
    /// <exception cref="ArgumentNullException">null ID(s)</exception>
    public void SetItemGroupId(IList<string> ids, string? groupId) =>
        Wait(SetItemGroupIdAsync(ids, groupId));

    /// <summary>
    /// Set the group ID of the item(s) with the specified ID(s).
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The items identifiers.</param>
    /// <param name="groupId">The group ID (can be null).</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID(s)</exception>
    public async Task SetItemGroupIdAsync(IList<string> ids, string? groupId,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var items = GetCollection<MongoItem>(MongoItem.COLLECTION);
        await items.UpdateManyAsync(
            Builders<MongoItem>.Filter.In(i => i.Id, ids),
            Builders<MongoItem>.Update.Set(i => i.GroupId, groupId),
            cancellationToken: cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the requested page from a list of all the distinct, non-null
    /// group IDs found in the items.
    /// </summary>
    /// <param name="options">The paging options. A page size of 0 means
    /// all the group IDs.</param>
    /// <param name="filter">The optional filter to be found inside any group
    /// ID (case insensitive).</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">options</exception>
    public async Task<DataPage<string>> GetDistinctGroupIdsAsync(
        PagingOptions options, string? filter = null)
    {
        #region MongoDB query
        // db.getCollection("items").aggregate([
        //   { "$match": { "groupId": { "$ne": null, "$regex": "...",
        //     "$options": "i" } } },
        //   { "$group": { "_id": "$groupId" } },
        //   { "$facet": {
        //       "total": [ { "$count": "count" } ],
        //       "page": [ { "$sort": { "_id": 1 } }, { "$skip": 0 },
        //                 { "$limit": 20 } ]
        //   } }
        // ]);
        #endregion

        ArgumentNullException.ThrowIfNull(options);

        var items = GetCollection<BsonDocument>(MongoItem.COLLECTION);

        // match before grouping, so that the groupId index can be used
        BsonDocument groupMatch = new("$ne", BsonNull.Value);
        if (!string.IsNullOrEmpty(filter))
        {
            groupMatch.Add("$regex", Regex.Escape(filter))
                .Add("$options", "i");
        }

        BsonArray pageStages = new()
        {
            new BsonDocument("$sort", new BsonDocument("_id", 1)),
            new BsonDocument("$skip", options.GetSkipCount())
        };
        if (options.PageSize > 0)
            pageStages.Add(new BsonDocument("$limit", options.PageSize));

        PipelineDefinition<BsonDocument, BsonDocument> pipeline =
            new BsonDocument[]
        {
            new("$match", new BsonDocument("groupId", groupMatch)),
            new("$group", new BsonDocument("_id", "$groupId")),
            new("$facet", new BsonDocument
            {
                { "total", new BsonArray
                    {
                        new BsonDocument("$count", "count")
                    }
                },
                { "page", pageStages }
            })
        };

        using IAsyncCursor<BsonDocument> cursor = await items.AggregateAsync(
            pipeline, new AggregateOptions { AllowDiskUse = true })
            .ConfigureAwait(false);
        BsonDocument? result = await cursor.FirstOrDefaultAsync()
            .ConfigureAwait(false);

        BsonArray? totalArray = result?["total"].AsBsonArray;
        int total = totalArray?.Count > 0
            ? totalArray[0]["count"].ToInt32() : 0;
        List<string> ids = total > 0
            ? [.. result!["page"].AsBsonArray.Select(d => d["_id"].AsString)]
            : [];

        return new DataPage<string>(options.PageNumber, options.PageSize,
            total, ids);
    }

    /// <summary>
    /// Get the count of all the non-empty layers in the specified items
    /// group.
    /// </summary>
    /// <param name="groupId">The group ID.</param>
    /// <returns>The count.</returns>
    /// <exception cref="ArgumentNullException">groupId</exception>
    public async Task<int> GetGroupLayersCountAsync(string groupId)
    {
        ArgumentNullException.ThrowIfNull(groupId);

        IMongoDatabase db = GetDatabase();

        // get the IDs of the items in the group
        List<string> itemIds = await db.GetCollection<MongoItem>(
            MongoItem.COLLECTION)
            .Find(i => i.GroupId == groupId)
            .Project(i => i.Id)
            .ToListAsync().ConfigureAwait(false);
        if (itemIds.Count == 0) return 0;

        // count their parts having a non-empty fragments array
        FilterDefinitionBuilder<BsonDocument> builder =
            Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> f =
            builder.In("itemId", itemIds)
            & builder.Exists("content.fragments")
            & builder.Ne("content.fragments", new BsonArray());

        return (int)await db.GetCollection<BsonDocument>(MongoPart.COLLECTION)
            .CountDocumentsAsync(f).ConfigureAwait(false);
    }

    private static FilterDefinition<MongoHistoryItem> BuildHistoryItemFilter(
        HistoryItemFilter filter)
    {
        FilterDefinitionBuilder<MongoHistoryItem> builder =
            Builders<MongoHistoryItem>.Filter;
        FilterDefinition<MongoHistoryItem> f = builder.Empty;

        // title and description: contains, case insensitive
        if (!string.IsNullOrEmpty(filter.Title))
        {
            f &= builder.Regex(i => i.Title,
                BuildContainsRegex(filter.Title, true));
        }

        if (!string.IsNullOrEmpty(filter.Description))
        {
            f &= builder.Regex(i => i.Description,
                BuildContainsRegex(filter.Description, true));
        }

        if (!string.IsNullOrEmpty(filter.FacetId))
            f &= builder.Eq(i => i.FacetId, filter.FacetId);

        if (!string.IsNullOrEmpty(filter.GroupId))
            f &= builder.Eq(i => i.GroupId, filter.GroupId);

        if (filter.Flags.HasValue)
        {
            f &= BuildFlagsFilter<MongoHistoryItem>(filter.FlagMatching,
                filter.Flags.Value);
        }

        if (!string.IsNullOrEmpty(filter.ReferenceId))
            f &= builder.Eq(i => i.ReferenceId, filter.ReferenceId);

        if (filter.Status.HasValue)
            f &= builder.Eq(i => i.Status, filter.Status.Value);

        if (!string.IsNullOrEmpty(filter.UserId))
            f &= builder.Eq(i => i.UserId, filter.UserId);

        if (filter.MinModified.HasValue)
            f &= builder.Gte(i => i.TimeModified, filter.MinModified.Value);

        if (filter.MaxModified.HasValue)
            f &= builder.Lte(i => i.TimeModified, filter.MaxModified.Value);

        return f;
    }

    /// <summary>
    /// Gets a page of history items.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>history items page</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    public DataPage<HistoryItemInfo> GetHistoryItems(HistoryItemFilter filter) =>
        Wait(GetHistoryItemsAsync(filter));

    /// <summary>
    /// Gets a page of history items, sorted by their modified time
    /// (descending), reference ID, and ID. Title and description are matched
    /// when they contain the filter's value (case insensitive); all the
    /// other filters (facet, group, reference, status, user) must be equal.
    /// </summary>
    /// <param name="filter">The filter. A page size of 0 means all the
    /// matching items.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>History items page.</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    public async Task<DataPage<HistoryItemInfo>> GetHistoryItemsAsync(
        HistoryItemFilter filter, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var items = GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION);
        FilterDefinition<MongoHistoryItem> f = BuildHistoryItemFilter(filter);

        int total = (int)await items.CountDocumentsAsync(f,
            cancellationToken: cancel).ConfigureAwait(false);
        if (total == 0)
        {
            return new DataPage<HistoryItemInfo>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<HistoryItemInfo>());
        }

        List<MongoHistoryItem> page = await ApplyPaging(items.Find(f)
            .SortByDescending(i => i.TimeModified)
            .ThenBy(i => i.ReferenceId)
            .ThenBy(i => i.Id), filter)
            .ToListAsync(cancel).ConfigureAwait(false);

        return new DataPage<HistoryItemInfo>(
            filter.PageNumber, filter.PageSize, total,
            page.ConvertAll(i => i.ToHistoryItemInfo()));
    }

    /// <summary>
    /// Gets the specified history item.
    /// </summary>
    /// <param name="id">The history item's identifier.</param>
    /// <returns>item or null if not found</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public HistoryItem? GetHistoryItem(string id) =>
        Wait(GetHistoryItemAsync(id));

    /// <summary>
    /// Gets the specified history item.
    /// </summary>
    /// <param name="id">The history item's identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Item or null if not found.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task<HistoryItem?> GetHistoryItemAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var items = GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION);
        MongoHistoryItem? item = await items.Find(i => i.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        return item?.ToHistoryItem();
    }

    /// <summary>
    /// Adds or updates the specified history item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException">null item</exception>
    public void AddHistoryItem(HistoryItem item) =>
        Wait(AddHistoryItemAsync(item));

    /// <summary>
    /// Adds or updates the specified history item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null item</exception>
    public async Task AddHistoryItemAsync(HistoryItem item,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        var items = GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION);
        await items.ReplaceOneAsync(h => h.Id == item.Id,
            new MongoHistoryItem(item),
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified history item.
    /// </summary>
    /// <param name="id">The history item's identifier.</param>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public void DeleteHistoryItem(string id) =>
        Wait(DeleteHistoryItemAsync(id));

    /// <summary>
    /// Deletes the specified history item.
    /// </summary>
    /// <param name="id">The history item's identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task DeleteHistoryItemAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var items = GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION);
        await items.DeleteOneAsync(i => i.Id == id, cancel)
            .ConfigureAwait(false);
    }

    private string SerializeItemForExport(IItem item, bool includeParts)
    {
        // serialize the item without parts, as parts are typed as IPart
        // and would lose all their type-specific properties
        JsonObject root = JsonSerializer.SerializeToNode(item, item.GetType(),
            _jsonOptions)!.AsObject();
        root.Remove(PARTS_PROPERTY);

        if (includeParts)
        {
            // serialize each part using its concrete type, exactly
            // like when storing it
            JsonArray parts = [];
            foreach (IPart part in item.Parts)
            {
                parts.Add(JsonSerializer.SerializeToNode(part, part.GetType(),
                    _jsonOptions));
            }
            root[PARTS_PROPERTY] = parts;
        }

        return root.ToJsonString(_exportJsonOptions);
    }

    /// <summary>
    /// Exports the item with the specified ID as a JSON document to the
    /// given writer. The document is the JSON serialization of the item,
    /// where the <c>parts</c> property (present only when
    /// <paramref name="includeParts"/> is true) is an array with the full
    /// JSON serialization of each part, including its <c>typeId</c> and
    /// <c>roleId</c>. This is the format expected by <see cref="ImportItem"/>.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="writer">The writer to export the item to. The writer
    /// is flushed but not closed.</param>
    /// <param name="includeParts">if set to <c>true</c>, include all the item's
    /// parts.</param>
    /// <returns>True if the item was found and exported, false if not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">null ID or writer</exception>
    public bool ExportItem(string id, TextWriter writer,
        bool includeParts = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(writer);

        IItem? item = GetItem(id, includeParts);
        if (item == null) return false;

        writer.Write(SerializeItemForExport(item, includeParts));
        writer.Flush();
        return true;
    }

    /// <summary>
    /// Exports the item with the specified ID as a JSON document to the
    /// given writer. See <see cref="ExportItem"/>.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="writer">The writer to export the item to. The writer
    /// is flushed but not closed.</param>
    /// <param name="includeParts">if set to <c>true</c>, include all the item's
    /// parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>True if the item was found and exported, false if not found.
    /// </returns>
    /// <exception cref="ArgumentNullException">null ID or writer</exception>
    public async Task<bool> ExportItemAsync(string id, TextWriter writer,
        bool includeParts = true, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(writer);

        IItem? item = await GetItemAsync(id, includeParts, cancel)
            .ConfigureAwait(false);
        if (item == null) return false;

        await writer.WriteAsync(
            SerializeItemForExport(item, includeParts).AsMemory(), cancel)
            .ConfigureAwait(false);
        await writer.FlushAsync(cancel).ConfigureAwait(false);
        return true;
    }

    private static string? GetStringProperty(JsonObject obj, string name)
    {
        if (!obj.TryGetPropertyValue(name, out JsonNode? node) || node == null)
            return null;

        if (node.GetValueKind() != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Property \"{name}\" must be a string");
        }
        return node.GetValue<string>();
    }

    private IPart ParseImportedPart(JsonNode? node, string itemId)
    {
        if (node is not JsonObject obj)
        {
            throw new InvalidDataException(
                $"Each part of item {itemId} must be a JSON object");
        }

        string? typeId = GetStringProperty(obj, "typeId");
        if (string.IsNullOrEmpty(typeId))
        {
            throw new InvalidDataException(
                $"Part without type ID in item {itemId}");
        }
        string? roleId = GetStringProperty(obj, "roleId");

        IPart part = InstantiatePart(typeId, roleId, obj.ToJsonString())
            ?? throw new InvalidDataException("Unknown part type " +
                $"{PartBase.BuildProviderId(typeId, roleId)} in item {itemId}");

        if (!string.IsNullOrEmpty(part.ItemId) && part.ItemId != itemId)
        {
            throw new InvalidDataException(
                $"Part {part.Id} belongs to item {part.ItemId} " +
                $"rather than to item {itemId}");
        }
        return part;
    }

    private Item ParseImportedItem(string json)
    {
        if (JsonNode.Parse(json) is not JsonObject root)
            throw new InvalidDataException("Item document must be a JSON object");

        // detach parts from the item, as they must be typed one by one
        JsonNode? partsNode = root[PARTS_PROPERTY];
        if (partsNode != null && partsNode is not JsonArray)
            throw new InvalidDataException("Item parts must be a JSON array");
        root.Remove(PARTS_PROPERTY);

        // the item ID is required to validate its parts
        if (string.IsNullOrEmpty(GetStringProperty(root, "id")))
            throw new InvalidDataException("Item without ID");

        Item item = root.Deserialize<Item>(_jsonOptions)
            ?? throw new InvalidDataException("Unable to deserialize item");
        if (string.IsNullOrEmpty(item.FacetId))
            throw new InvalidDataException($"Item {item.Id} without facet ID");
        item.Title ??= "";
        item.Description ??= "";
        item.SortKey ??= "";
        item.CreatorId ??= "";
        item.UserId ??= "";

        if (partsNode is JsonArray parts)
        {
            item.Parts = [.. parts.Select(p => ParseImportedPart(p, item.Id))];
        }
        return item;
    }

    /// <summary>
    /// Imports a new item with its parts from a JSON document read from the
    /// given reader, as produced by <see cref="ExportItem"/>. An imported
    /// item is always a new record: importing an item whose ID is already
    /// present in the database is not allowed. Should the item or any of its
    /// parts have an ID found in history (e.g. because they were deleted),
    /// or any part have an ID already in use, they get a new ID, as deleted
    /// records cannot be resurrected. The whole document is validated before
    /// saving anything.
    /// </summary>
    /// <param name="reader">The reader to import the item from.</param>
    /// <param name="userId">The ID of the user performing the import. When
    /// specified, this is set as the creator and user ID of the item and of
    /// its parts.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <returns>The imported item with its parts, having their final IDs.
    /// </returns>
    /// <exception cref="ArgumentNullException">null reader</exception>
    /// <exception cref="JsonException">invalid JSON</exception>
    /// <exception cref="InvalidDataException">invalid item document
    /// </exception>
    /// <exception cref="InvalidOperationException">item already exists
    /// </exception>
    public IItem ImportItem(TextReader reader, string? userId = null,
        bool history = true)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Item item = ParseImportedItem(reader.ReadToEnd());
        return Wait(SaveImportedItemAsync(item, userId, history,
            CancellationToken.None));
    }

    /// <summary>
    /// Imports a new item with its parts from a JSON document read from the
    /// given reader. See <see cref="ImportItem"/>.
    /// </summary>
    /// <param name="reader">The reader to import the item from.</param>
    /// <param name="userId">The ID of the user performing the import. When
    /// specified, this is set as the creator and user ID of the item and of
    /// its parts.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The imported item with its parts, having their final IDs.
    /// </returns>
    /// <exception cref="ArgumentNullException">null reader</exception>
    /// <exception cref="JsonException">invalid JSON</exception>
    /// <exception cref="InvalidDataException">invalid item document
    /// </exception>
    /// <exception cref="InvalidOperationException">item already exists
    /// </exception>
    public async Task<IItem> ImportItemAsync(TextReader reader,
        string? userId = null, bool history = true,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(reader);

        Item item = ParseImportedItem(
            await reader.ReadToEndAsync(cancel).ConfigureAwait(false));
        return await SaveImportedItemAsync(item, userId, history, cancel)
            .ConfigureAwait(false);
    }

    private async Task<IItem> SaveImportedItemAsync(Item item, string? userId,
        bool history, CancellationToken cancel)
    {
        IMongoDatabase db = GetDatabase();

        if (await db.GetCollection<MongoItem>(MongoItem.COLLECTION)
            .Find(i => i.Id == item.Id).Limit(1)
            .AnyAsync(cancel).ConfigureAwait(false))
        {
            throw new InvalidOperationException(
                $"Item {item.Id} already exists");
        }

        // a deleted item cannot be resurrected: if its ID is in history,
        // the imported item is a new record with its own new ID
        if (await db.GetCollection<MongoHistoryItem>(MongoHistoryItem.COLLECTION)
            .Find(h => h.ReferenceId == item.Id).Limit(1)
            .AnyAsync(cancel).ConfigureAwait(false))
        {
            item.Id = Guid.NewGuid().ToString();
        }

        // the same holds for parts, whose IDs must also be unique
        List<string> ids = item.Parts.ConvertAll(p => p.Id);
        HashSet<string> usedIds = [];
        if (ids.Count > 0)
        {
            usedIds.UnionWith(await db.GetCollection<MongoPart>(
                MongoPart.COLLECTION)
                .Find(Builders<MongoPart>.Filter.In(p => p.Id, ids))
                .Project(p => p.Id)
                .ToListAsync(cancel).ConfigureAwait(false));
            usedIds.UnionWith(await db.GetCollection<MongoHistoryPart>(
                MongoHistoryPart.COLLECTION)
                .Find(Builders<MongoHistoryPart>.Filter.In(
                    h => h.ReferenceId, ids))
                .Project(h => h.ReferenceId)
                .ToListAsync(cancel).ConfigureAwait(false));
        }

        // save as a newly created item with newly created parts
        DateTime now = DateTime.UtcNow;
        item.TimeCreated = now;
        if (userId != null) item.CreatorId = item.UserId = userId;
        await AddItemAsync(item, history, cancel).ConfigureAwait(false);

        foreach (IPart part in item.Parts)
        {
            // Add fails for empty IDs, IDs in use, and duplicates in the file
            if (string.IsNullOrEmpty(part.Id) || !usedIds.Add(part.Id))
                part.Id = Guid.NewGuid().ToString();
            part.ItemId = item.Id;
            part.TimeCreated = now;
            if (userId != null) part.CreatorId = part.UserId = userId;
            await AddPartAsync(part, history, cancel).ConfigureAwait(false);
        }

        return item;
    }
    #endregion

    #region Parts
    private IPart? InstantiatePart(string typeId, string? roleId, string content)
    {
        string reqTypeId = PartBase.BuildProviderId(typeId, roleId);
        Type? type = _partTypeProvider.Get(reqTypeId);
        if (type == null) return null;

        return (IPart?)JsonSerializer.Deserialize(content, type, _jsonOptions);
    }

    private IPart InstantiatePart(MongoPart part) =>
        InstantiatePart(part.TypeId, part.RoleId,
            part.Content.ToJson(_jsonSettings))
        ?? throw new InvalidOperationException(
            "Unable to instantiate part for type.role=" +
            $"{part.TypeId}.{part.RoleId}");

    private List<IPart> InstantiateParts(IEnumerable<MongoPart> parts)
    {
        List<IPart> results = [];

        foreach (MongoPart mongoPart in parts)
        {
            IPart? part = InstantiatePart(mongoPart.TypeId, mongoPart.RoleId,
                mongoPart.Content.ToJson(_jsonSettings));

            if (part == null)
            {
                string error = "Unable to instantiate part for type.role=" +
                    $"{mongoPart.TypeId}.{mongoPart.RoleId}";
                Debug.WriteLine(error);
                throw new InvalidOperationException(error);
            }
            results.Add(part);
        }
        return results;
    }

    private static FilterDefinition<MongoPart> BuildPartFilter(
        IList<string>? itemIds, string? typeId, string? roleId,
        string? thesaurusScope)
    {
        FilterDefinitionBuilder<MongoPart> builder = Builders<MongoPart>.Filter;
        FilterDefinition<MongoPart> f = builder.Empty;

        if (itemIds?.Count > 0)
        {
            f &= itemIds.Count == 1
                ? builder.Eq(p => p.ItemId, itemIds[0])
                : builder.In(p => p.ItemId, itemIds);
        }

        if (!string.IsNullOrEmpty(typeId))
            f &= builder.Eq(p => p.TypeId, typeId);

        if (!string.IsNullOrEmpty(roleId))
            f &= builder.Eq(p => p.RoleId, roleId);

        if (!string.IsNullOrEmpty(thesaurusScope))
            f &= builder.Eq(p => p.ThesaurusScope, thesaurusScope);

        return f;
    }

    /// <summary>
    /// Gets the specified page of parts.
    /// </summary>
    /// <param name="filter">The parts filter.</param>
    /// <returns>parts page</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    public DataPage<PartInfo> GetParts(PartFilter filter) =>
        Wait(GetPartsAsync(filter));

    /// <summary>
    /// Gets the specified page of parts. Parts are sorted by type and role
    /// ID, unless <see cref="PartFilter.SortExpressions"/> are specified:
    /// each of them has the name of a part property (case insensitive,
    /// e.g. <c>TypeId</c>), optionally followed by a path into the part's
    /// content (e.g. <c>Content.y</c>), and true for ascending order.
    /// </summary>
    /// <param name="filter">The parts filter. A page size of 0 means all the
    /// matching parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Parts page.</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    /// <exception cref="ArgumentException">unknown sort property</exception>
    public async Task<DataPage<PartInfo>> GetPartsAsync(PartFilter filter,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        FilterDefinitionBuilder<MongoPart> builder = Builders<MongoPart>.Filter;
        FilterDefinition<MongoPart> f = BuildPartFilter(filter.ItemIds,
            filter.TypeId, filter.RoleId, filter.ThesaurusScope);

        if (!string.IsNullOrEmpty(filter.UserId))
            f &= builder.Eq(p => p.UserId, filter.UserId);

        if (filter.MinModified.HasValue)
            f &= builder.Gte(p => p.TimeModified, filter.MinModified.Value);

        if (filter.MaxModified.HasValue)
            f &= builder.Lte(p => p.TimeModified, filter.MaxModified.Value);

        // build the sort before querying, so that errors are thrown early
        SortDefinition<MongoPart> sort = filter.SortExpressions?.Count > 0
            ? BuildSort<MongoPart>(filter.SortExpressions)
            : Builders<MongoPart>.Sort
                .Ascending(p => p.TypeId)
                .Ascending(p => p.RoleId)
                .Ascending(p => p.Id);

        var parts = GetCollection<MongoPart>(MongoPart.COLLECTION);
        int total = (int)await parts.CountDocumentsAsync(f,
            cancellationToken: cancel).ConfigureAwait(false);
        if (total == 0)
        {
            return new DataPage<PartInfo>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<PartInfo>());
        }

        // the content is not required for part infos
        List<MongoPart> page = await ApplyPaging(parts.Find(f).Sort(sort),
            filter)
            .Project<MongoPart>(Builders<MongoPart>.Projection
                .Exclude(p => p.Content))
            .ToListAsync(cancel).ConfigureAwait(false);

        return new DataPage<PartInfo>(
            filter.PageNumber, filter.PageSize, total,
            page.ConvertAll(p => p.ToPartInfo()));
    }

    /// <summary>
    /// Gets the parts belonging to the specified item(s), eventually
    /// filtered by their type and/or role.
    /// </summary>
    /// <param name="itemIds">The item ID(s).</param>
    /// <param name="typeId">The optional type identifier.</param>
    /// <param name="roleId">The optional role identifier.</param>
    /// <returns>parts</returns>
    /// <exception cref="ArgumentNullException">null item ID(s)</exception>
    /// <exception cref="ArgumentOutOfRangeException">empty item IDs array
    /// </exception>
    public IList<IPart> GetItemParts(string[] itemIds, string? typeId = null,
        string? roleId = null) =>
        Wait(GetItemPartsAsync(itemIds, typeId, roleId));

    /// <summary>
    /// Gets the parts belonging to the specified item(s), eventually
    /// filtered by their type and/or role, sorted by type and role.
    /// </summary>
    /// <param name="itemIds">The item ID(s).</param>
    /// <param name="typeId">The optional type identifier.</param>
    /// <param name="roleId">The optional role identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Parts.</returns>
    /// <exception cref="ArgumentNullException">null item ID(s)</exception>
    /// <exception cref="ArgumentOutOfRangeException">empty item IDs array
    /// </exception>
    /// <exception cref="InvalidOperationException">unable to instantiate part
    /// </exception>
    public async Task<IList<IPart>> GetItemPartsAsync(string[] itemIds,
        string? typeId = null, string? roleId = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(itemIds);
        if (itemIds.Length == 0)
            throw new ArgumentOutOfRangeException(nameof(itemIds));

        var collection = GetCollection<MongoPart>(MongoPart.COLLECTION);
        List<MongoPart> parts = await collection
            .Find(BuildPartFilter(itemIds, typeId, roleId, null))
            .SortBy(p => p.TypeId)
            .ThenBy(p => p.RoleId)
            .ToListAsync(cancel).ConfigureAwait(false);

        return InstantiateParts(parts);
    }

    private static DateTime GetBsonDateTime(BsonDocument doc, string name) =>
        doc.TryGetValue(name, out BsonValue value) && value.IsValidDateTime
            ? value.ToUniversalTime()
            : DateTime.MinValue;

    private static string? GetBsonString(BsonDocument doc, string name) =>
        doc.TryGetValue(name, out BsonValue value) && value.IsString
            ? value.AsString
            : null;

    /// <summary>
    /// Gets layer parts information about the item with the specified ID.
    /// </summary>
    /// <param name="itemId">The item's identifier.</param>
    /// <param name="absent">if set to <c>true</c>, include also information
    /// about absent parts, i.e. those parts which are not present in the
    /// repository, but are defined in the item's facet.</param>
    /// <returns>layers parts information.</returns>
    /// <exception cref="ArgumentNullException">itemId</exception>
    public IList<LayerPartInfo> GetItemLayerInfo(string itemId, bool absent) =>
        Wait(GetItemLayerInfoAsync(itemId, absent));

    /// <summary>
    /// Gets layer parts information about the item with the specified ID.
    /// </summary>
    /// <param name="itemId">The item's identifier.</param>
    /// <param name="absent">if set to <c>true</c>, include also information
    /// about absent parts, i.e. those parts which are not present in the
    /// repository, but are defined in the item's facet.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Layers parts information, sorted by role.</returns>
    /// <exception cref="ArgumentNullException">itemId</exception>
    public async Task<IList<LayerPartInfo>> GetItemLayerInfoAsync(
        string itemId, bool absent, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);

        IMongoDatabase db = GetDatabase();

        // get all the layer parts for the specified item; their content
        // (which can be big) is not loaded, just counting their fragments
        PipelineDefinition<BsonDocument, BsonDocument> pipeline =
            new BsonDocument[]
        {
            new("$match", new BsonDocument
            {
                { "itemId", itemId },
                { "roleId", new BsonRegularExpression(
                    "^" + Regex.Escape(PartBase.FR_PREFIX)) }
            }),
            new("$project", new BsonDocument
            {
                { "itemId", 1 },
                { "typeId", 1 },
                { "roleId", 1 },
                { "thesaurusScope", 1 },
                { "timeCreated", 1 },
                { "creatorId", 1 },
                { "timeModified", 1 },
                { "userId", 1 },
                { "fragmentCount", new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$isArray", "$content.fragments"),
                        new BsonDocument("$size", "$content.fragments"),
                        0
                    })
                }
            }),
            new("$sort", new BsonDocument { { "roleId", 1 }, { "_id", 1 } })
        };

        using IAsyncCursor<BsonDocument> cursor = await db
            .GetCollection<BsonDocument>(MongoPart.COLLECTION)
            .AggregateAsync(pipeline, cancellationToken: cancel)
            .ConfigureAwait(false);
        List<BsonDocument> docs = await cursor.ToListAsync(cancel)
            .ConfigureAwait(false);

        // generate the corresponding layer part infos
        List<LayerPartInfo> results = docs.ConvertAll(d => new LayerPartInfo
        {
            Id = d["_id"].AsString,
            ItemId = GetBsonString(d, "itemId")!,
            TypeId = GetBsonString(d, "typeId")!,
            RoleId = GetBsonString(d, "roleId"),
            ThesaurusScope = GetBsonString(d, "thesaurusScope"),
            TimeCreated = GetBsonDateTime(d, "timeCreated"),
            CreatorId = GetBsonString(d, "creatorId") ?? "",
            TimeModified = GetBsonDateTime(d, "timeModified"),
            UserId = GetBsonString(d, "userId") ?? "",
            FragmentCount = d["fragmentCount"].ToInt32()
        });

        // append to these results the absent layer parts if requested
        if (absent)
        {
            // get the facet from the item's facet ID
            string? facetId = await db.GetCollection<MongoItem>(
                MongoItem.COLLECTION)
                .Find(i => i.Id == itemId)
                .Project(i => i.FacetId)
                .FirstOrDefaultAsync(cancel).ConfigureAwait(false);

            if (facetId != null)
            {
                MongoFacetDefinition? facet = await db
                    .GetCollection<MongoFacetDefinition>(
                        MongoFacetDefinition.COLLECTION)
                    .Find(f => f.Id == facetId)
                    .FirstOrDefaultAsync(cancel).ConfigureAwait(false);

                if (facet != null)
                {
                    DateTime now = DateTime.UtcNow;

                    // append each layer part definition not already present
                    foreach (PartDefinition def in facet.PartDefinitions
                        // must be a layer part
                        .Where(pd => pd.RoleId?.StartsWith(
                            PartBase.FR_PREFIX, StringComparison.Ordinal) == true
                        // must not be already present in results
                        && !results.Exists(i => i.RoleId == pd.RoleId
                                           && i.TypeId == pd.TypeId))
                        .OrderBy(pd => pd.RoleId))
                    {
                        results.Add(new LayerPartInfo
                        {
                            Id = null,
                            ItemId = itemId,
                            TypeId = def.TypeId,
                            RoleId = def.RoleId,
                            TimeCreated = now,
                            CreatorId = "",
                            TimeModified = now,
                            UserId = "",
                            IsAbsent = true
                        });
                    }
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the specified part.
    /// </summary>
    /// <typeparam name="T">The type of the part to cast the result to.
    /// </typeparam>
    /// <param name="id">The part identifier.</param>
    /// <returns>part or null if not found</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public T? GetPart<T>(string id) where T : class, IPart =>
        Wait(GetPartAsync<T>(id));

    /// <summary>
    /// Gets the specified part.
    /// </summary>
    /// <typeparam name="T">The type of the part to cast the result to.
    /// </typeparam>
    /// <param name="id">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Part or null if not found.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    /// <exception cref="InvalidOperationException">unable to instantiate part
    /// </exception>
    /// <exception cref="InvalidCastException">part not of type
    /// <typeparamref name="T"/></exception>
    public async Task<T?> GetPartAsync<T>(string id,
        CancellationToken cancel = default) where T : class, IPart
    {
        ArgumentNullException.ThrowIfNull(id);

        MongoPart? part = await GetCollection<MongoPart>(MongoPart.COLLECTION)
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (part == null) return null;

        return (T)InstantiatePart(part);
    }

    /// <summary>
    /// Gets the identifier of the item including the part with the
    /// specified part identifier.
    /// </summary>
    /// <param name="id">The part identifier.</param>
    /// <returns>
    /// The item identifier, or null if part not found
    /// </returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public string? GetPartItemId(string id) => Wait(GetPartItemIdAsync(id));

    /// <summary>
    /// Gets the identifier of the item including the part with the
    /// specified part identifier.
    /// </summary>
    /// <param name="id">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The item identifier, or null if part not found.</returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public async Task<string?> GetPartItemIdAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await GetCollection<MongoPart>(MongoPart.COLLECTION)
            .Find(p => p.Id == id)
            .Project(p => p.ItemId)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the JSON code representing the part with the specified ID.
    /// </summary>
    /// <param name="id">The part identifier.</param>
    /// <returns>JSON code or null if not found</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public string? GetPartContent(string id) => Wait(GetPartContentAsync(id));

    /// <summary>
    /// Gets the JSON code representing the part with the specified ID.
    /// </summary>
    /// <param name="id">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>JSON code or null if not found.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task<string?> GetPartContentAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        MongoPart? part = await GetCollection<MongoPart>(MongoPart.COLLECTION)
            .Find(p => p.Id == id)
            .Project<MongoPart>(Builders<MongoPart>.Projection
                .Include(p => p.Content))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);

        return part?.Content?.ToJson(_jsonSettings);
    }

    /// <summary>
    /// Adds or updates the specified part.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <exception cref="ArgumentNullException">null part</exception>
    public void AddPart(IPart part, bool history = true) =>
        Wait(AddPartAsync(part, history));

    /// <summary>
    /// Adds or updates the specified part. Its modified time is set to now.
    /// If the part already exists, its creator and creation time are kept.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null part</exception>
    public async Task AddPartAsync(IPart part, bool history = true,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(part);

        IMongoDatabase db = GetDatabase();
        var parts = db.GetCollection<MongoPart>(MongoPart.COLLECTION);

        // creator and creation time cannot be changed once set
        MongoPart? old = await parts.Find(p => p.Id == part.Id)
            .Project<MongoPart>(Builders<MongoPart>.Projection
                .Include(p => p.CreatorId)
                .Include(p => p.TimeCreated))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (old?.CreatorId != null)
        {
            part.CreatorId = old.CreatorId;
            part.TimeCreated = old.TimeCreated;
        }
        // set time modified
        part.TimeModified = DateTime.UtcNow;

        string json = JsonSerializer.Serialize(part, part.GetType(),
            _jsonOptions);

        MongoPart mongoPart = new(part)
        {
            Content = BsonDocument.Parse(json)
        };

        if (history)
        {
            MongoHistoryPart historyPart = new(part)
            {
                Content = mongoPart.Content,
                Status = old != null ? EditStatus.Updated : EditStatus.Created
            };

            await db.GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
                .InsertOneAsync(historyPart, cancellationToken: cancel)
                .ConfigureAwait(false);
        }

        await parts.ReplaceOneAsync(p => p.Id == part.Id,
            mongoPart,
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    private IPart ParsePartContent(string content)
    {
        string? typeId, roleId = null;
        using (JsonDocument doc = JsonDocument.Parse(content))
        {
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("typeId", out JsonElement tid)
                || tid.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Part without type ID in content: " + content);
            }
            typeId = tid.GetString()!;

            if (doc.RootElement.TryGetProperty("roleId", out JsonElement rid)
                && rid.ValueKind == JsonValueKind.String)
            {
                roleId = rid.GetString();
            }
        }

        return InstantiatePart(typeId, roleId, content)
            ?? throw new InvalidOperationException(
                $"Unable to instantiate part with type ID {typeId} " +
                $"and role ID {roleId} from content: " + content);
    }

    /// <summary>
    /// Adds or updates the specified part from its encoded content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <exception cref="ArgumentNullException">content</exception>
    /// <exception cref="InvalidOperationException">content without type ID,
    /// or unable to instantiate its part</exception>
    public void AddPartFromContent(string content, bool history = true) =>
        Wait(AddPartFromContentAsync(content, history));

    /// <summary>
    /// Adds or updates the specified part from its encoded content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">content</exception>
    /// <exception cref="InvalidOperationException">content without type ID,
    /// or unable to instantiate its part</exception>
    public Task AddPartFromContentAsync(string content, bool history = true,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        return AddPartAsync(ParsePartContent(content), history, cancel);
    }

    /// <summary>
    /// Deletes the specified part.
    /// </summary>
    /// <param name="id">The part's identifier.</param>
    /// <param name="userId">The identifier of the user deleting the part.
    /// </param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <exception cref="ArgumentNullException">null part ID or user ID
    /// </exception>
    public void DeletePart(string id, string userId, bool history = true) =>
        Wait(DeletePartAsync(id, userId, history));

    /// <summary>
    /// Deletes the specified part.
    /// </summary>
    /// <param name="id">The part's identifier.</param>
    /// <param name="userId">The identifier of the user deleting the part.
    /// </param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null part ID or user ID
    /// </exception>
    public async Task DeletePartAsync(string id, string userId,
        bool history = true, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);

        IMongoDatabase db = GetDatabase();
        var parts = db.GetCollection<MongoPart>(MongoPart.COLLECTION);

        MongoPart? part = await parts.Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (part == null) return;

        if (history)
        {
            MongoHistoryPart historyPart = new(part)
            {
                // user deleted it now
                UserId = userId,
                Status = EditStatus.Deleted,
                TimeModified = DateTime.UtcNow
            };

            await db.GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
                .InsertOneAsync(historyPart, cancellationToken: cancel)
                .ConfigureAwait(false);
        }

        await parts.DeleteOneAsync(p => p.Id == id, cancel)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gets a page of history parts.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>history items page</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    public DataPage<HistoryPartInfo> GetHistoryParts(HistoryPartFilter filter) =>
        Wait(GetHistoryPartsAsync(filter));

    /// <summary>
    /// Gets a page of history parts. These are sorted by their modified time
    /// (descending) and ID, unless
    /// <see cref="HistoryPartFilter.SortExpressions"/> are specified, as
    /// for <see cref="GetPartsAsync"/>.
    /// </summary>
    /// <param name="filter">The filter. A page size of 0 means all the
    /// matching parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>History parts page.</returns>
    /// <exception cref="ArgumentNullException">null filter</exception>
    /// <exception cref="ArgumentException">unknown sort property</exception>
    public async Task<DataPage<HistoryPartInfo>> GetHistoryPartsAsync(
        HistoryPartFilter filter, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        FilterDefinitionBuilder<MongoHistoryPart> builder =
            Builders<MongoHistoryPart>.Filter;
        FilterDefinition<MongoHistoryPart> f = builder.Empty;

        if (filter.ItemIds?.Count > 0)
        {
            f &= filter.ItemIds.Count == 1
                ? builder.Eq(p => p.ItemId, filter.ItemIds[0])
                : builder.In(p => p.ItemId, filter.ItemIds);
        }

        if (!string.IsNullOrEmpty(filter.TypeId))
            f &= builder.Eq(p => p.TypeId, filter.TypeId);

        if (!string.IsNullOrEmpty(filter.RoleId))
            f &= builder.Eq(p => p.RoleId, filter.RoleId);

        if (!string.IsNullOrEmpty(filter.ReferenceId))
            f &= builder.Eq(p => p.ReferenceId, filter.ReferenceId);

        if (filter.Status.HasValue)
            f &= builder.Eq(p => p.Status, filter.Status.Value);

        if (!string.IsNullOrEmpty(filter.UserId))
            f &= builder.Eq(p => p.UserId, filter.UserId);

        if (filter.MinModified.HasValue)
            f &= builder.Gte(p => p.TimeModified, filter.MinModified.Value);

        if (filter.MaxModified.HasValue)
            f &= builder.Lte(p => p.TimeModified, filter.MaxModified.Value);

        SortDefinition<MongoHistoryPart> sort =
            filter.SortExpressions?.Count > 0
            ? BuildSort<MongoHistoryPart>(filter.SortExpressions)
            : Builders<MongoHistoryPart>.Sort
                .Descending(p => p.TimeModified)
                .Ascending(p => p.Id);

        var parts = GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION);
        int total = (int)await parts.CountDocumentsAsync(f,
            cancellationToken: cancel).ConfigureAwait(false);
        if (total == 0)
        {
            return new DataPage<HistoryPartInfo>(
                filter.PageNumber, filter.PageSize, 0,
                Array.Empty<HistoryPartInfo>());
        }

        // the content is not required for part infos
        List<MongoHistoryPart> page = await ApplyPaging(
            parts.Find(f).Sort(sort), filter)
            .Project<MongoHistoryPart>(Builders<MongoHistoryPart>.Projection
                .Exclude(p => p.Content))
            .ToListAsync(cancel).ConfigureAwait(false);

        return new DataPage<HistoryPartInfo>(
            filter.PageNumber, filter.PageSize, total,
            page.ConvertAll(p => p.ToHistoryPartInfo()));
    }

    /// <summary>
    /// Gets the specified history part.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>part or null if not found</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public HistoryPart<T>? GetHistoryPart<T>(string id)
        where T : class, IPart =>
        Wait(GetHistoryPartAsync<T>(id));

    /// <summary>
    /// Gets the specified history part.
    /// </summary>
    /// <typeparam name="T">The type of the part to cast the result to.
    /// This can also be just <see cref="IPart"/>, as the part is
    /// instantiated from its type ID and role ID.</typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Part or null if not found.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    /// <exception cref="InvalidOperationException">unable to instantiate part
    /// </exception>
    /// <exception cref="InvalidCastException">part not of type
    /// <typeparamref name="T"/></exception>
    public async Task<HistoryPart<T>?> GetHistoryPartAsync<T>(string id,
        CancellationToken cancel = default) where T : class, IPart
    {
        ArgumentNullException.ThrowIfNull(id);

        MongoHistoryPart? part = await GetCollection<MongoHistoryPart>(
            MongoHistoryPart.COLLECTION)
            .Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (part == null) return null;

        IPart wrapped = InstantiatePart(part.TypeId, part.RoleId,
            part.Content.ToJson(_jsonSettings))
            ?? throw new InvalidOperationException(
                "Unable to instantiate part for type.role=" +
                $"{part.TypeId}.{part.RoleId}");

        return new HistoryPart<T>(part.Id, (T)wrapped)
        {
            ReferenceId = part.ReferenceId,
            TimeModified = part.TimeModified,
            UserId = part.UserId,
            Status = part.Status
        };
    }

    /// <summary>
    /// Deletes the specified history part.
    /// </summary>
    /// <param name="id">The history part's identifier.</param>
    /// <exception cref="ArgumentNullException">null part ID</exception>
    public void DeleteHistoryPart(string id) =>
        Wait(DeleteHistoryPartAsync(id));

    /// <summary>
    /// Deletes the specified history part.
    /// </summary>
    /// <param name="id">The history part's identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null part ID</exception>
    public async Task DeleteHistoryPartAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        await GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
            .DeleteOneAsync(p => p.Id == id, cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the ID of the creator of the part with the specified ID.
    /// </summary>
    /// <param name="id">The part ID.</param>
    /// <returns>Creator ID, or null.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public string? GetPartCreatorId(string id) =>
        Wait(GetPartCreatorIdAsync(id));

    /// <summary>
    /// Gets the ID of the creator of the part with the specified ID.
    /// </summary>
    /// <param name="id">The part ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Creator ID, or null if the part was not found.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public async Task<string?> GetPartCreatorIdAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await GetCollection<MongoPart>(MongoPart.COLLECTION)
            .Find(p => p.Id == id)
            .Project(p => p.CreatorId)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Finds the latest version of the base text part with the specified ID
    /// in history whose text differs from that of its preceding version.
    /// If the text never changed, this is its oldest version.
    /// </summary>
    /// <returns>Tuple with the history part and the total count of history
    /// versions of the base text part, or null if no history, or if a
    /// version could not be instantiated as a part with text.</returns>
    private async Task<Tuple<MongoHistoryPart, int>?> FindLastBaseTextChangeAsync(
        IMongoDatabase db, string baseTextPartId, CancellationToken cancel)
    {
        var historyParts = db.GetCollection<MongoHistoryPart>(
            MongoHistoryPart.COLLECTION);
        FilterDefinition<MongoHistoryPart> f =
            Builders<MongoHistoryPart>.Filter.Eq(p => p.ReferenceId,
                baseTextPartId);

        int total = (int)await historyParts.CountDocumentsAsync(f,
            cancellationToken: cancel).ConfigureAwait(false);
        if (total == 0) return null;

        // walk versions from the newest to the oldest, comparing each
        // with its preceding one, until the text changes
        MongoHistoryPart? newer = null;
        string? newerText = null;

        using IAsyncCursor<MongoHistoryPart> cursor = await historyParts
            .Find(f)
            .SortByDescending(p => p.TimeModified)
            .ToCursorAsync(cancel).ConfigureAwait(false);

        while (await cursor.MoveNextAsync(cancel).ConfigureAwait(false))
        {
            foreach (MongoHistoryPart hp in cursor.Current)
            {
                if (InstantiatePart(hp.TypeId, hp.RoleId,
                    hp.Content.ToJson(_jsonSettings)) is not IHasText part)
                {
                    return null;
                }
                string? text = part.GetText();

                if (newer != null && text != newerText)
                    return Tuple.Create(newer, total);

                newer = hp;
                newerText = text;
            }
        }

        // the text never changed: the reference is the oldest version
        return newer != null ? Tuple.Create(newer, total) : null;
    }

    /// <summary>
    /// Determines whether the layer part with the specified ID might
    /// potentially have been broken because of changes in its base text.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="toleranceSeconds">The count of seconds representing
    /// the tolerance time interval between a base text save time and that
    /// of its layer part. Once this interval has elapsed, the layer part
    /// is not considered as potentially broken. If set to 0 or less, no
    /// tolerance interval is allowed.</param>
    /// <returns>
    /// <c>0</c> if the layer part is not potentially broken; <c>1</c>
    /// if it's potentially broken; <c>2</c> if it's surely broken.
    /// </returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public int GetLayerPartBreakChance(string id, int toleranceSeconds) =>
        Wait(GetLayerPartBreakChanceAsync(id, toleranceSeconds));

    /// <summary>
    /// Determines whether the layer part with the specified ID might
    /// potentially have been broken because of changes in its base text.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="toleranceSeconds">The count of seconds representing
    /// the tolerance time interval between a base text save time and that
    /// of its layer part. Once this interval has elapsed, the layer part
    /// is not considered as potentially broken. If set to 0 or less, no
    /// tolerance interval is allowed.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>
    /// <c>0</c> if the layer part is not potentially broken; <c>1</c>
    /// if it's potentially broken; <c>2</c> if it's surely broken.
    /// </returns>
    /// <remarks>A layer part is potentially broken when the corresponding
    /// text part has been saved (with a different text) either after it,
    /// or a few time before it.
    /// In both cases, this implies that the part fragments might have
    /// broken links, as the underlying text was in some way changed.
    /// To detect a potential break we can just check for last modified date
    /// and time; if the above conditions for save date and time are not met,
    /// the method can return false. If instead they are met, we must ensure
    /// that text has changed. To this end, we must go back in the text part
    /// history to find the latest save which changed the text, and refer
    /// to its date and time. If the history of the text part includes a
    /// single record, this means that the text part is in its original
    /// state; in this case, we never consider the layer part as broken,
    /// because this means that we just have imported or seeded both the
    /// text part and its layer(s) at the same time, or within a few
    /// fractions of seconds.
    /// </remarks>
    /// <exception cref="ArgumentNullException">id</exception>
    public async Task<int> GetLayerPartBreakChanceAsync(string id,
        int toleranceSeconds, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        IMongoDatabase db = GetDatabase();
        var parts = db.GetCollection<MongoPart>(MongoPart.COLLECTION);

        // get the layer part item ID and its last modified time
        // (if there is no such part, there's nothing which can be broken!)
        MongoPart? layerPart = await parts.Find(p => p.Id == id)
            .Project<MongoPart>(Builders<MongoPart>.Projection
                .Include(p => p.ItemId)
                .Include(p => p.TimeModified))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (layerPart == null) return 0;

        // get the layer part's container item
        // (if not found, it's broken for sure: the whole part is orphan!
        // anyway, this should never happen, as deleting items implying
        // deleting all their parts)
        MongoItem? item = await db.GetCollection<MongoItem>(MongoItem.COLLECTION)
            .Find(i => i.Id == layerPart.ItemId)
            .Project<MongoItem>(Builders<MongoItem>.Projection
                .Include(i => i.FacetId))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (item == null) return 2;

        // get the item's facet
        bool hasFacet = await db.GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION)
            .Find(f => f.Id == item.FacetId).Limit(1)
            .AnyAsync(cancel).ConfigureAwait(false);
        if (!hasFacet) return 1; // defensive

        // get the base text part from its role
        // (if not found, it's broken for sure, as any other layer in this item:
        // in this case, all the layers are orphaned)
        MongoPart? baseTextPart = await parts
            .Find(p => p.ItemId == layerPart.ItemId
                && p.RoleId == PartBase.BASE_TEXT_ROLE_ID)
            .Project<MongoPart>(Builders<MongoPart>.Projection
                .Include(p => p.TimeModified))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (baseTextPart == null) return 2;

        // determine the reference text part save time:
        // - if we have history, look for the latest saved base text
        // version where the text changed from its preceding version,
        // and use it as a reference.
        // - else, just use the current base text part as the reference.
        var lastHistoryPartAndTotal = await FindLastBaseTextChangeAsync(
            db, baseTextPart.Id, cancel).ConfigureAwait(false);
        DateTime lastTextChange = lastHistoryPartAndTotal != null
            ? lastHistoryPartAndTotal.Item1.TimeModified
            : baseTextPart.TimeModified;

        // the total count of text part versions in history, used to determine
        // if the current text part state is original (which happens when
        // the count is 1); consider it as 2 if there is no history at all,
        // because in such cases we cannot know if the current (unique)
        // state is original or not.
        int historyTotal = lastHistoryPartAndTotal != null
            ? lastHistoryPartAndTotal.Item2 : 2;

        // with/without tolerance: potentially broken if
        // last text change happened after/when layer was saved.
        if (lastTextChange >= layerPart.TimeModified && historyTotal > 1)
            return 1;

        // last text change happened before layer was saved:
        // potentially broken if within the tolerance interval.
        return toleranceSeconds > 0 && historyTotal > 1
            && (layerPart.TimeModified - lastTextChange).TotalSeconds
               <= toleranceSeconds ? 1 : 0;
    }

    /// <summary>
    /// Gets the layer part reconciliation hints.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The hints, one for each fragment in the layer part.
    /// If any of the required resources is not found, an empty list
    /// will be returned.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    public IList<LayerHint> GetLayerPartHints(string id) =>
        Wait(GetLayerPartHintsAsync(id));

    /// <summary>
    /// Gets the layer part reconciliation hints. These are calculated by
    /// comparing the current base text with the base text when the layer
    /// part was last saved, i.e. its latest version in history saved before
    /// the layer part.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The hints, one for each fragment in the layer part.
    /// If any of the required resources is not found, an empty list
    /// will be returned.</returns>
    /// <exception cref="ArgumentNullException">null ID</exception>
    /// <exception cref="InvalidOperationException">unable to instantiate the
    /// base text part</exception>
    public async Task<IList<LayerHint>> GetLayerPartHintsAsync(string id,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        IMongoDatabase db = GetDatabase();
        var parts = db.GetCollection<MongoPart>(MongoPart.COLLECTION);
        List<LayerHint> hints = [];

        // get the layer part
        MongoPart? mongoLayerPart = await parts.Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (mongoLayerPart == null) return hints;

        // get the layer part's container item
        string? facetId = await db.GetCollection<MongoItem>(MongoItem.COLLECTION)
            .Find(i => i.Id == mongoLayerPart.ItemId)
            .Project(i => i.FacetId)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (facetId == null) return hints;

        // get the item's facet
        bool hasFacet = await db.GetCollection<MongoFacetDefinition>(
            MongoFacetDefinition.COLLECTION)
            .Find(f => f.Id == facetId).Limit(1)
            .AnyAsync(cancel).ConfigureAwait(false);
        if (!hasFacet) return hints;

        // get the base text part from its role
        MongoPart? mongoTextPart = await parts
            .Find(p => p.ItemId == mongoLayerPart.ItemId
                && p.RoleId == PartBase.BASE_TEXT_ROLE_ID)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (mongoTextPart == null) return hints;

        // get the current text
        IPart textPart = InstantiatePart(mongoTextPart);
        string? currentText = (textPart as IHasText)?.GetText();
        if (currentText == null) return hints;

        // locate the text which was current when the layer part
        // was last saved, i.e. its latest version in history whose save
        // time is less than the layer part save time
        MongoHistoryPart? hp = await db.GetCollection<MongoHistoryPart>(
            MongoHistoryPart.COLLECTION)
            .Find(p => p.ReferenceId == textPart.Id
                  && p.TimeModified < mongoLayerPart.TimeModified)
            .SortByDescending(p => p.TimeModified)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (hp == null) return hints;

        // get the old text
        IPart? oldTextPart = InstantiatePart(hp.TypeId, hp.RoleId,
            hp.Content.ToJson(_jsonSettings));
        string? oldText = (oldTextPart as IHasText)?.GetText();
        if (oldText == null) return hints;

        // calculate diff's between old and new text
        List<Diff> diffs = new diff_match_patch()
            .diff_main(oldText, currentText);

        // adapt them into YX-based edit operations (the adapter is
        // created lazily here if required)
        _opDiffAdapter ??= new YXEditOperationDiffAdapter();
        IList<YXEditOperation> operations = _opDiffAdapter.Adapt(diffs);

        // to create hints, just load the layer part as a stripped down
        // part whose fragments just include their locations. This is all
        // what is needed for our purposes here, and is required because
        // we do not know at compile time the type of the fragment
        AnonLayerPart anonLayerPart = JsonSerializer.Deserialize<AnonLayerPart>(
            mongoLayerPart.Content.ToJson(_jsonSettings), _jsonOptions)!;

        return anonLayerPart.GetFragmentHints(operations);
    }

    /// <summary>
    /// Applies the specified patches instructions to the layer part
    /// with ID equal to <paramref name="id" />.
    /// </summary>
    /// <param name="id">The layer part identifier.</param>
    /// <param name="userId">The user identifier. This will be set as
    /// the author of the changes in the part.</param>
    /// <param name="patches">The patch instructions.</param>
    /// <returns>The patched layer part content or null if layer part not
    /// found.</returns>
    /// <exception cref="ArgumentNullException">id or userId or patches
    /// </exception>
    public string? ApplyLayerPartPatches(string id, string userId,
        IList<string> patches) =>
        Wait(ApplyLayerPartPatchesAsync(id, userId, patches));

    /// <summary>
    /// Applies the specified patches instructions to the layer part
    /// with ID equal to <paramref name="id" />. The patched part is saved
    /// as modified now by the specified user, and stored in history as
    /// updated.
    /// </summary>
    /// <param name="id">The layer part identifier.</param>
    /// <param name="userId">The user identifier. This will be set as
    /// the author of the changes in the part.</param>
    /// <param name="patches">The patch instructions.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The patched layer part content or null if layer part not
    /// found.</returns>
    /// <exception cref="ArgumentNullException">id or userId or patches
    /// </exception>
    public async Task<string?> ApplyLayerPartPatchesAsync(string id,
        string userId, IList<string> patches,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(userId);
        ArgumentNullException.ThrowIfNull(patches);

        IMongoDatabase db = GetDatabase();

        // get the layer part
        var parts = db.GetCollection<MongoPart>(MongoPart.COLLECTION);
        MongoPart? mongoLayerPart = await parts.Find(p => p.Id == id)
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (mongoLayerPart == null) return null;

        // instantiate a generic layer part
        string json = mongoLayerPart.Content.ToJson(_jsonSettings);
        AnonLayerPart anonLayerPart = JsonSerializer.Deserialize<AnonLayerPart>(
            json, _jsonOptions)!;

        // apply patches to it, and set its author and time, both in
        // the part and in its content
        DateTime now = DateTime.UtcNow;
        BsonDocument content = BsonDocument.Parse(
            anonLayerPart.ApplyPatches(json, patches));
        content["userId"] = userId;
        content["timeModified"] = SerializeDateTime(now);

        mongoLayerPart.Content = content;
        mongoLayerPart.UserId = userId;
        mongoLayerPart.TimeModified = now;

        // save it back (unless it was deleted in the meantime)
        ReplaceOneResult result = await parts.ReplaceOneAsync(
            p => p.Id == mongoLayerPart.Id,
            mongoLayerPart,
            new ReplaceOptions { IsUpsert = false },
            cancel).ConfigureAwait(false);
        if (result.MatchedCount == 0) return null;

        await db.GetCollection<MongoHistoryPart>(MongoHistoryPart.COLLECTION)
            .InsertOneAsync(new MongoHistoryPart(mongoLayerPart)
            {
                Status = EditStatus.Updated
            }, cancellationToken: cancel).ConfigureAwait(false);

        // return patched part content
        return content.ToJson(_jsonSettings);
    }

    /// <summary>
    /// Set the <see cref="IPart.ThesaurusScope"/> property of all the
    /// parts with the specified ID.
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The part identifier(s).</param>
    /// <param name="scope">The new scope (may be null).</param>
    /// <exception cref="ArgumentNullException">null ID(s)</exception>
    public void SetPartThesaurusScope(IList<string> ids, string? scope) =>
        Wait(SetPartThesaurusScopeAsync(ids, scope));

    /// <summary>
    /// Set the <see cref="IPart.ThesaurusScope"/> property of all the
    /// parts with the specified ID, both in the part and in its content.
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The part identifier(s).</param>
    /// <param name="scope">The new scope (may be null).</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null ID(s)</exception>
    public async Task SetPartThesaurusScopeAsync(IList<string> ids,
        string? scope, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0) return;

        await GetCollection<MongoPart>(MongoPart.COLLECTION).UpdateManyAsync(
            Builders<MongoPart>.Filter.In(p => p.Id, ids),
            Builders<MongoPart>.Update
                .Set(p => p.ThesaurusScope, scope)
                .Set("content.thesaurusScope",
                    scope != null ? (BsonValue)scope : BsonNull.Value),
            cancellationToken: cancel).ConfigureAwait(false);
    }
    #endregion

    #region Settings
    /// <summary>
    /// Adds or updates the settings with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="json">The JSON code representing the settings value.</param>
    /// <exception cref="ArgumentNullException">null or empty key or json</exception>
    public void AddSetting(string key, string json) =>
        Wait(AddSettingAsync(key, json));

    /// <summary>
    /// Adds or updates the settings with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="json">The JSON code representing the settings value.
    /// This must be a JSON object.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null or empty key or json
    /// </exception>
    /// <exception cref="FormatException">invalid JSON object</exception>
    public async Task AddSettingAsync(string key, string json,
        CancellationToken cancel = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        ArgumentException.ThrowIfNullOrEmpty(json);

        // parse the JSON content into a BsonDocument with the key as its ID
        BsonDocument document = BsonDocument.Parse(json);
        document["_id"] = key;

        // add or update the document with the specified key
        await GetCollection<BsonDocument>(SETTINGS_COLLECTION).ReplaceOneAsync(
            new BsonDocument("_id", key),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancel).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets the setting with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The JSON code representing the settings value, or null
    /// if not found.</returns>
    /// <exception cref="ArgumentNullException">null or empty key</exception>
    public string? GetSetting(string key) => Wait(GetSettingAsync(key));

    /// <summary>
    /// Gets the setting with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The JSON code representing the settings value, or null
    /// if not found.</returns>
    /// <exception cref="ArgumentNullException">null or empty key</exception>
    public async Task<string?> GetSettingAsync(string key,
        CancellationToken cancel = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        BsonDocument? document = await GetCollection<BsonDocument>(
            SETTINGS_COLLECTION)
            .Find(Builders<BsonDocument>.Filter.Eq("_id", key))
            .FirstOrDefaultAsync(cancel).ConfigureAwait(false);
        if (document == null) return null;

        // the ID is the key, not part of the value
        document.Remove("_id");
        return document.ToJson(_jsonSettings);
    }

    /// <summary>
    /// Deletes the setting with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <exception cref="ArgumentNullException">null or empty key</exception>
    public void DeleteSetting(string key) => Wait(DeleteSettingAsync(key));

    /// <summary>
    /// Deletes the setting with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">null or empty key</exception>
    public async Task DeleteSettingAsync(string key,
        CancellationToken cancel = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);

        await GetCollection<BsonDocument>(SETTINGS_COLLECTION)
            .DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", key),
                cancel).ConfigureAwait(false);
    }
    #endregion
}

/// <summary>
/// Options for <see cref="MongoCadmusRepository"/>.
/// </summary>
public sealed class MongoCadmusRepositoryOptions
{
    /// <summary>
    /// Gets or sets the MongoDB connection string, like e.g.
    /// <c>mongodb://localhost:27017/cadmus</c>.
    /// </summary>
    public string? ConnectionString { get; set; }
}
