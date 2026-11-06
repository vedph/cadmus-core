using Cadmus.Export.Mapping;
using Fusi.Tools.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Cadmus.Graph.Ef;

public abstract partial class EfGraphRepository
{
    private const string CACHE_KEY_PREFIX = "nm-";
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    // incremented whenever mappings change in this process, so that all
    // the cached mappings get stale
    private static int _cacheVersion;

    /// <summary>
    /// Gets or sets the optional cache to use for mappings. This improves
    /// performance when fetching the mappings to run for each saved item
    /// or part, which are cached by source type. The cache is invalidated
    /// whenever mappings are changed via any repository in this process;
    /// changes made by other processes (e.g. by the CLI tool) are seen when
    /// cached entries expire (see <see cref="MappingCacheDuration"/>).
    /// </summary>
    public IMemoryCache? Cache { get; set; }

    /// <summary>
    /// Gets or sets the duration of the mappings in <see cref="Cache"/>.
    /// The default is 5 minutes.
    /// </summary>
    public TimeSpan MappingCacheDuration { get; set; } = TimeSpan.FromMinutes(5);

    private static string GetCacheKey(string suffix) =>
        $"{CACHE_KEY_PREFIX}{Volatile.Read(ref _cacheVersion)}-{suffix}";

    private static void InvalidateCache() =>
        Interlocked.Increment(ref _cacheVersion);

    private static IQueryable<EfMapping> WithOutputs(
        IQueryable<EfMapping> mappings) => mappings
            .Include(m => m.MetaOutputs)
            .Include(m => m.NodeOutputs)
            .Include(m => m.TripleOutputs)
            .AsSplitQuery();

    private static GraphNodeMapping BuildTree(EfMapping mapping, int parentId,
        Dictionary<int, EfMapping> mappings, ILookup<int, int> children,
        HashSet<int> path)
    {
        GraphNodeMapping result = mapping.ToNodeMapping(parentId);

        // the path guards against cycles in links
        path.Add(mapping.Id);
        foreach (EfMapping child in children[mapping.Id]
            .Where(id => !path.Contains(id))
            .Select(id => mappings.GetValueOrDefault(id))
            .OfType<EfMapping>()
            .OrderBy(m => m.Ordinal)
            .ThenBy(m => m.Id))
        {
            result.Children.Add(BuildTree(child, mapping.Id, mappings,
                children, path));
        }
        path.Remove(mapping.Id);

        return result;
    }

    /// <summary>
    /// Loads the specified mappings with all their descendants, in a fixed
    /// number of queries whatever the depth of the mappings trees.
    /// </summary>
    private static async Task<List<GraphNodeMapping>> LoadTreesAsync(
        CadmusGraphDbContext context, IList<EfMapping> mappings,
        CancellationToken cancel)
    {
        if (mappings.Count == 0) return [];

        // links are few, so just get all of them
        ILookup<int, int> children = (await context.MappingLinks
            .AsNoTracking()
            .ToListAsync(cancel))
            .ToLookup(l => l.ParentId, l => l.ChildId);

        // collect the IDs of all the descendants
        HashSet<int> ids = [];
        Stack<int> stack = new(mappings.Select(m => m.Id));
        while (stack.Count > 0)
        {
            foreach (int childId in children[stack.Pop()])
            {
                if (ids.Add(childId)) stack.Push(childId);
            }
        }

        Dictionary<int, EfMapping> loaded = ids.Count == 0
            ? []
            : await WithOutputs(context.Mappings.AsNoTracking()
                .Where(m => ids.Contains(m.Id)))
                .ToDictionaryAsync(m => m.Id, cancel);
        foreach (EfMapping mapping in mappings)
            loaded.TryAdd(mapping.Id, mapping);

        return [.. mappings.Select(m => BuildTree(m, 0, loaded, children, []))];
    }

    private static bool IsRegexMatch(string? input, string pattern)
    {
        try
        {
            return Regex.IsMatch(input ?? "", pattern, RegexOptions.None,
                _regexTimeout);
        }
        catch (ArgumentException)
        {
            // an invalid regular expression in a mapping must not break
            // updates for all the other mappings
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            return false;
        }
    }

    /// <summary>
    /// Determines whether the specified root mapping should run for the
    /// source described by the specified filter. Each filter defined in
    /// the mapping must be satisfied by the source: facet, part type and
    /// part role must be equal (case-insensitively); group and title must
    /// match the mapping's regular expression; flags must include all the
    /// flags of the mapping.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="filter">The filter.</param>
    /// <returns>True if matching.</returns>
    /// <exception cref="ArgumentNullException">mapping or filter</exception>
    public static bool IsMatch(NodeMapping mapping, RunNodeMappingFilter filter)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(filter);

        if (mapping.SourceType != filter.SourceType) return false;

        if (!string.IsNullOrEmpty(mapping.FacetFilter) &&
            !string.Equals(mapping.FacetFilter, filter.Facet,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(mapping.GroupFilter) &&
            !IsRegexMatch(filter.Group, mapping.GroupFilter))
        {
            return false;
        }

        if (mapping.FlagsFilter.HasValue &&
            ((filter.Flags ?? 0) & mapping.FlagsFilter.Value)
                != mapping.FlagsFilter.Value)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(mapping.TitleFilter) &&
            !IsRegexMatch(filter.Title, mapping.TitleFilter))
        {
            return false;
        }

        if (!string.IsNullOrEmpty(mapping.PartTypeFilter) &&
            !string.Equals(mapping.PartTypeFilter, filter.PartType,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrEmpty(mapping.PartRoleFilter) ||
            string.Equals(mapping.PartRoleFilter, filter.PartRole,
                StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the specified page of node mappings.
    /// </summary>
    /// <param name="filter">The filter. Set page size=0 to get all
    /// the mappings at once.</param>
    /// <param name="descendants">True to load the descendants of each mapping.
    /// </param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<GraphNodeMapping> GetMappings(NodeMappingFilter filter,
        bool descendants)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();

        // mappings are few, so filter them in memory: this is also
        // consistent with the regular expressions used when running them
        IQueryable<EfMapping> query = context.Mappings.AsNoTracking();
        query = filter.ParentId != null
            ? query.Where(m => m.ParentLinks!.Any(
                l => l.ParentId == filter.ParentId))
            : query.Where(m => !m.ParentLinks!.Any());
        if (filter.SourceType != null)
            query = query.Where(m => m.SourceType == filter.SourceType);

        IEnumerable<EfMapping> mappings = query.AsEnumerable();

        if (!string.IsNullOrEmpty(filter.Name))
        {
            mappings = mappings.Where(m => m.Name.Contains(filter.Name,
                StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(filter.Facet))
        {
            mappings = mappings.Where(m => string.Equals(m.FacetFilter,
                filter.Facet, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(filter.Group))
        {
            mappings = mappings.Where(m => m.GroupFilter != null &&
                IsRegexMatch(m.GroupFilter, filter.Group));
        }
        if (filter.Flags.HasValue)
        {
            mappings = mappings.Where(m => m.FlagsFilter.HasValue &&
                (m.FlagsFilter.Value & filter.Flags.Value) == filter.Flags.Value);
        }
        if (!string.IsNullOrEmpty(filter.Title))
        {
            mappings = mappings.Where(m => m.TitleFilter != null &&
                IsRegexMatch(m.TitleFilter, filter.Title));
        }
        if (!string.IsNullOrEmpty(filter.PartType))
        {
            mappings = mappings.Where(m => string.Equals(m.PartTypeFilter,
                filter.PartType, StringComparison.OrdinalIgnoreCase));
        }
        if (!string.IsNullOrEmpty(filter.PartRole))
        {
            mappings = mappings.Where(m => string.Equals(m.PartRoleFilter,
                filter.PartRole, StringComparison.OrdinalIgnoreCase));
        }

        List<EfMapping> matches = [.. mappings
            .OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(m => m.Id)];
        if (matches.Count == 0)
        {
            return new DataPage<GraphNodeMapping>(
                filter.PageNumber, filter.PageSize, 0, []);
        }

        IEnumerable<EfMapping> page = matches.Skip(filter.GetSkipCount());
        if (filter.PageSize > 0) page = page.Take(filter.PageSize);
        List<int> pageIds = [.. page.Select(m => m.Id)];

        // load the outputs for the page
        Dictionary<int, EfMapping> full = WithOutputs(context.Mappings
            .AsNoTracking()
            .Where(m => pageIds.Contains(m.Id)))
            .ToDictionary(m => m.Id);
        List<EfMapping> results = [.. pageIds.Select(id => full[id])];

        List<GraphNodeMapping> items = descendants
            ? LoadTreesAsync(context, results, default).GetAwaiter().GetResult()
            : results.ConvertAll(m => m.ToNodeMapping(filter.ParentId ?? 0));

        return new DataPage<GraphNodeMapping>(filter.PageNumber,
            filter.PageSize, matches.Count, items);
    }

    /// <summary>
    /// Gets the node mapping with the specified ID, including all its
    /// descendant mappings.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The mapping or null if not found.</returns>
    public GraphNodeMapping? GetMapping(int id)
    {
        using CadmusGraphDbContext context = GetContext();

        EfMapping? mapping = WithOutputs(context.Mappings.AsNoTracking()
            .Where(m => m.Id == id))
            .FirstOrDefault();
        if (mapping == null) return null;

        return LoadTreesAsync(context, [mapping], default)
            .GetAwaiter().GetResult()[0];
    }

    /// <summary>
    /// Deletes the specified mappings, and then all their descendants
    /// which are no longer children of any other mapping.
    /// </summary>
    private static async Task DeleteMappingsAsync(CadmusGraphDbContext context,
        IList<int> ids, CancellationToken cancel)
    {
        if (ids.Count == 0) return;

        // collect descendants before deleting the links
        ILookup<int, int> children = (await context.MappingLinks
            .AsNoTracking()
            .ToListAsync(cancel))
            .ToLookup(l => l.ParentId, l => l.ChildId);
        HashSet<int> descendants = [];
        Stack<int> stack = new(ids);
        while (stack.Count > 0)
        {
            foreach (int childId in children[stack.Pop()])
            {
                if (descendants.Add(childId)) stack.Push(childId);
            }
        }
        descendants.ExceptWith(ids);

        // delete mappings (their links and outputs are deleted in cascade)
        await context.Mappings.Where(m => ids.Contains(m.Id))
            .ExecuteDeleteAsync(cancel);

        // delete orphaned descendants, level by level
        while (descendants.Count > 0)
        {
            List<int> candidates = [.. descendants];
            List<int> orphans = await context.Mappings
                .Where(m => candidates.Contains(m.Id) && !m.ParentLinks!.Any())
                .Select(m => m.Id)
                .ToListAsync(cancel);
            if (orphans.Count == 0) break;

            await context.Mappings.Where(m => orphans.Contains(m.Id))
                .ExecuteDeleteAsync(cancel);
            descendants.ExceptWith(orphans);
        }
    }

    private static void CopyMapping(EfMapping source, EfMapping target)
    {
        target.Ordinal = source.Ordinal;
        target.Name = source.Name;
        target.SourceType = source.SourceType;
        target.FacetFilter = source.FacetFilter;
        target.GroupFilter = source.GroupFilter;
        target.FlagsFilter = source.FlagsFilter;
        target.TitleFilter = source.TitleFilter;
        target.PartTypeFilter = source.PartTypeFilter;
        target.PartRoleFilter = source.PartRoleFilter;
        target.Description = source.Description;
        target.Source = source.Source;
        target.Sid = source.Sid;
        target.ScalarPattern = source.ScalarPattern;

        // outputs: replace
        target.MetaOutputs ??= [];
        target.MetaOutputs.Clear();
        if (source.MetaOutputs != null)
        {
            foreach (EfMappingMetaOutput o in source.MetaOutputs)
                o.Mapping = target;
            target.MetaOutputs.AddRange(source.MetaOutputs);
        }

        target.NodeOutputs ??= [];
        target.NodeOutputs.Clear();
        if (source.NodeOutputs != null)
        {
            foreach (EfMappingNodeOutput o in source.NodeOutputs)
                o.Mapping = target;
            target.NodeOutputs.AddRange(source.NodeOutputs);
        }

        target.TripleOutputs ??= [];
        target.TripleOutputs.Clear();
        if (source.TripleOutputs != null)
        {
            foreach (EfMappingTripleOutput o in source.TripleOutputs)
                o.Mapping = target;
            target.TripleOutputs.AddRange(source.TripleOutputs);
        }
    }

    /// <summary>
    /// Adds or updates the specified child mapping with all its descendants.
    /// Named children are shared: a child with the same name of an existing
    /// child mapping (in any tree) updates it, so that mappings reused
    /// across different trees (e.g. named mappings in a mappings document)
    /// are stored once. Unnamed children are always added.
    /// </summary>
    private static async Task AddOrUpdateChildMappingAsync(
        GraphNodeMapping mapping, EfMapping parent,
        CadmusGraphDbContext context, CancellationToken cancel)
    {
        EfMapping source = new(mapping);
        EfMapping? target = null;

        if (!string.IsNullOrEmpty(mapping.Name))
        {
            target = await context.Mappings
                .Include(m => m.MetaOutputs)
                .Include(m => m.NodeOutputs)
                .Include(m => m.TripleOutputs)
                .AsSplitQuery()
                .Where(m => m.Name == mapping.Name && m.ParentLinks!.Any())
                .OrderBy(m => m.Id)
                .FirstOrDefaultAsync(cancel);
        }

        List<int> oldChildIds = [];
        if (target != null)
        {
            CopyMapping(source, target);

            // its children are replaced by the new ones
            oldChildIds = await context.MappingLinks
                .Where(l => l.ParentId == target.Id)
                .Select(l => l.ChildId)
                .ToListAsync(cancel);
            await context.MappingLinks.Where(l => l.ParentId == target.Id)
                .ExecuteDeleteAsync(cancel);
            await context.SaveChangesAsync(cancel);
        }
        else
        {
            target = source;
            target.Id = 0;
            context.Mappings.Add(target);
            await context.SaveChangesAsync(cancel);
        }
        mapping.Id = target.Id;

        // add parent-child link
        if (!await context.MappingLinks.AnyAsync(
            l => l.ParentId == parent.Id && l.ChildId == target.Id, cancel))
        {
            context.MappingLinks.Add(new EfMappingLink
            {
                ParentId = parent.Id,
                ChildId = target.Id
            });
            await context.SaveChangesAsync(cancel);
        }

        // recursively add/update children
        if (mapping.HasChildren)
        {
            foreach (GraphNodeMapping child in mapping.Children
                .Cast<GraphNodeMapping>())
            {
                await AddOrUpdateChildMappingAsync(child, target, context,
                    cancel);
            }
        }

        // remove the old children which are no longer used anywhere
        if (oldChildIds.Count > 0)
        {
            List<int> orphans = await context.Mappings
                .Where(m => oldChildIds.Contains(m.Id) && !m.ParentLinks!.Any())
                .Select(m => m.Id)
                .ToListAsync(cancel);
            await DeleteMappingsAsync(context, orphans, cancel);
        }
    }

    /// <summary>
    /// Adds the specified root mapping, replacing any existing root mapping
    /// with the same name. Its children are added, or updated when a child
    /// mapping with the same name already exists.
    /// </summary>
    /// <param name="mapping">The mapping. Its ID will be updated if newly
    /// added.</param>
    /// <returns>The mapping ID.</returns>
    /// <exception cref="ArgumentNullException">mapping</exception>
    /// <exception cref="InvalidOperationException">not a root mapping
    /// </exception>
    public int AddMapping(GraphNodeMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        if (mapping.ParentId != 0)
            throw new InvalidOperationException($"Not a root mapping: {mapping}");

        using CadmusGraphDbContext context = GetContext();
        try
        {
            return RunInTransactionAsync(context, async () =>
            {
                // remove the root mapping with the same name if any, with
                // all its descendants not shared with other mappings
                List<int> oldIds = await context.Mappings
                    .Where(m => m.Name == mapping.Name && !m.ParentLinks!.Any())
                    .Select(m => m.Id)
                    .ToListAsync();
                await DeleteMappingsAsync(context, oldIds, default);

                // add new root mapping
                EfMapping root = new(mapping) { Id = 0 };
                context.Mappings.Add(root);
                await context.SaveChangesAsync();
                mapping.Id = root.Id;

                // add or update all the children recursively
                if (mapping.HasChildren)
                {
                    foreach (GraphNodeMapping child in mapping.Children
                        .Cast<GraphNodeMapping>())
                    {
                        await AddOrUpdateChildMappingAsync(child, root,
                            context, default);
                    }
                }
                return root.Id;
            }, default).GetAwaiter().GetResult();
        }
        finally
        {
            InvalidateCache();
        }
    }

    /// <summary>
    /// Deletes the specified node mapping, with all its descendants which
    /// are not shared with other mappings.
    /// </summary>
    /// <param name="id">The mapping identifier.</param>
    public void DeleteMapping(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        try
        {
            RunInTransactionAsync(context,
                () => DeleteMappingsAsync(context, [id], default),
                default).GetAwaiter().GetResult();
        }
        finally
        {
            InvalidateCache();
        }
    }

    /// <summary>
    /// Finds all the root mappings applicable to the specified source,
    /// with all their descendants, sorted by their ordinal value.
    /// </summary>
    /// <param name="filter">The filter to match.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>List of mappings. When using a <see cref="Cache"/>, these
    /// objects are shared, and must not be modified.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public async Task<IList<GraphNodeMapping>> FindMappingsAsync(
        RunNodeMappingFilter filter, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        string key = GetCacheKey($"roots-{filter.SourceType}");
        if (Cache == null ||
            !Cache.TryGetValue(key, out IList<GraphNodeMapping>? roots) ||
            roots == null)
        {
            await using CadmusGraphDbContext context = GetContext();
            List<EfMapping> mappings = await WithOutputs(context.Mappings
                .AsNoTracking()
                .Where(m => m.SourceType == filter.SourceType &&
                    !m.ParentLinks!.Any()))
                .OrderBy(m => m.Ordinal)
                .ThenBy(m => m.Id)
                .ToListAsync(cancel);
            roots = await LoadTreesAsync(context, mappings, cancel);

            Cache?.Set(key, roots, MappingCacheDuration);
        }

        return [.. roots.Where(m => IsMatch(m, filter))];
    }

    /// <summary>
    /// Finds all the root mappings applicable to the specified source
    /// (see <see cref="FindMappingsAsync"/>).
    /// </summary>
    /// <param name="filter">The filter to match.</param>
    /// <returns>List of mappings.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public IList<GraphNodeMapping> FindMappings(RunNodeMappingFilter filter)
        => FindMappingsAsync(filter).GetAwaiter().GetResult();

    private static JsonSerializerOptions GetMappingJsonSerializerOptions()
    {
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true,
        };
        options.Converters.Add(new NodeMappingOutputJsonConverter());
        options.Converters.Add(new NodeMappingJsonConverter<GraphNodeMapping>());
        return options;
    }

    /// <summary>
    /// Imports mappings from the specified JSON code representing a mappings
    /// document.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <returns>The number of root mappings imported.</returns>
    /// <exception cref="ArgumentNullException">json</exception>
    /// <exception cref="InvalidDataException">Invalid JSON mappings document
    /// </exception>
    public int Import(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        NodeMappingDocument doc =
            JsonSerializer.Deserialize<NodeMappingDocument>(json,
            GetMappingJsonSerializerOptions())
            ?? throw new InvalidDataException("Invalid JSON mappings document");

        int n = 0;
        foreach (GraphNodeMapping mapping in doc.GetMappings()
            .Cast<GraphNodeMapping>())
        {
            AddMapping(mapping);
            n++;
        }
        return n;
    }

    /// <summary>
    /// Exports all the mappings to JSON code representing a mappings
    /// document.
    /// </summary>
    /// <returns>JSON.</returns>
    public string Export()
    {
        NodeMappingDocument doc = new();
        DataPage<GraphNodeMapping> page = GetMappings(new NodeMappingFilter
        {
            PageSize = 0
        }, true);
        doc.DocumentMappings.AddRange(page.Items);

        return JsonSerializer.Serialize(doc, GetMappingJsonSerializerOptions());
    }
}
