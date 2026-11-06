using Cadmus.Core.Config;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Caching.Memory;
using System.Globalization;
using System.Text;

namespace Cadmus.Graph.Ef;

/// <summary>
/// Base class for Entity-Framework-based graph repositories.
/// <para>Implementations are thread-safe: each operation uses its own
/// DB context, and the operations changing the graph are executed in
/// transactions. Concurrency between different processes or threads
/// (e.g. several users saving at the same time) is handled with the
/// locks provided by <see cref="LockAsync"/>, which derived classes should
/// implement with the database engine's locking mechanism.</para>
/// </summary>
public abstract partial class EfGraphRepository : IUidBuilder,
    IConfigurable<EfGraphRepositoryOptions>
{
    /// <summary>
    /// The lock space for graph sources (items or parts). Updates to the
    /// graph generated from the same source are serialized.
    /// </summary>
    protected const int LOCK_SOURCE = 0x43470001;

    /// <summary>
    /// The lock space for URIs: insertions of the same URI are serialized.
    /// </summary>
    protected const int LOCK_URI = 0x43470002;

    /// <summary>
    /// The lock space for UIDs: insertions of the same unsuffixed UID are
    /// serialized.
    /// </summary>
    protected const int LOCK_UID = 0x43470003;

    // maximum lengths of the database columns
    protected const int MAX_URI = 500;
    protected const int MAX_LABEL = 500;
    protected const int MAX_SID = 500;
    protected const int MAX_TAG = 50;
    protected const int MAX_LIT_TYPE = 100;
    protected const int MAX_LIT_LANG = 10;
    protected const int MAX_LIT_IX = 15000;

    private const int CLASS_BATCH_SIZE = 100;

    // IDs of rdf:type and rdfs:subClassOf: these never change in a database
    private (int A, int Sub)? _aSubIds;

    /// <summary>
    /// Gets the connection string.
    /// </summary>
    protected string? ConnectionString { get; set; }

    /// <summary>
    /// Configures this repository with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(EfGraphRepositoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ConnectionString = options.ConnectionString;
        _aSubIds = null;
    }

    /// <summary>
    /// Gets a new DB context configured for <see cref="ConnectionString"/>.
    /// </summary>
    /// <returns>DB context.</returns>
    protected abstract CadmusGraphDbContext GetContext();

    #region Helpers
    protected static string? Truncate(string? text, int max) =>
        text == null || text.Length <= max ? text : text[..max];

    private static string EscapeLike(string text) => text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    /// <summary>
    /// Gets the pattern for a case-insensitive "contains" LIKE match with
    /// <c>\</c> as the escape character: this must be compared with the
    /// lowercased field value.
    /// </summary>
    /// <param name="text">The text to find.</param>
    /// <returns>Pattern.</returns>
    protected static string GetContainsPattern(string text) =>
        "%" + EscapeLike(text.ToLowerInvariant()) + "%";

    /// <summary>
    /// Acquires the locks for the specified keys in the specified space,
    /// releasing them at the end of the current transaction of
    /// <paramref name="context"/>. Implementations must lock the keys
    /// in a consistent order (to avoid deadlocks), and treat the keys
    /// case-insensitively. The default implementation does nothing, which
    /// is fine only when there are no concurrent writers.
    /// </summary>
    /// <param name="context">The context, with an active transaction.
    /// </param>
    /// <param name="space">The lock space, one of the <c>LOCK_</c>
    /// constants.</param>
    /// <param name="keys">The keys to lock.</param>
    /// <param name="cancel">The cancellation token.</param>
    protected virtual Task LockAsync(CadmusGraphDbContext context, int space,
        IEnumerable<string> keys, CancellationToken cancel)
        => Task.CompletedTask;

    /// <summary>
    /// Inserts the specified nodes unless they already exist. Nodes are
    /// identified by their ID, which must exist in the URIs lookup.
    /// Existing nodes are not updated. The default implementation uses
    /// Entity Framework: derived classes should override it to provide an
    /// atomic insertion, safe for concurrent writers.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="nodes">The nodes, sorted by ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    protected virtual async Task InsertMissingNodesAsync(
        CadmusGraphDbContext context, IList<Node> nodes,
        CancellationToken cancel)
    {
        if (nodes.Count == 0) return;
        List<int> ids = [.. nodes.Select(n => n.Id).Distinct()];
        HashSet<int> existing = [.. await context.Nodes.AsNoTracking()
            .Where(n => ids.Contains(n.Id)).Select(n => n.Id)
            .ToListAsync(cancel)];

        HashSet<int> added = [];
        foreach (Node node in nodes)
        {
            if (existing.Contains(node.Id) || !added.Add(node.Id)) continue;
            context.Nodes.Add(new EfNode(node)
            {
                Label = Truncate(node.Label, MAX_LABEL),
                Tag = Truncate(node.Tag, MAX_TAG),
                Sid = Truncate(node.Sid, MAX_SID)
            });
        }
        await context.SaveChangesAsync(cancel);
    }

    /// <summary>
    /// Populates the node classes for the nodes with the specified IDs.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="nodeIds">The node IDs.</param>
    /// <param name="aId">The ID of <c>rdf:type</c>.</param>
    /// <param name="subId">The ID of <c>rdfs:subClassOf</c>.</param>
    /// <param name="cancel">The cancellation token.</param>
    protected virtual async Task PopulateNodeClassesAsync(
        CadmusGraphDbContext context, IEnumerable<int> nodeIds, int aId,
        int subId, CancellationToken cancel)
    {
        foreach (int[] chunk in nodeIds.Distinct().Order()
            .Chunk(CLASS_BATCH_SIZE))
        {
            // only integers are concatenated here, so this is safe
            StringBuilder sb = new();
            foreach (int id in chunk)
            {
                sb.Append(CultureInfo.InvariantCulture,
                    $"CALL populate_node_class({id},{aId},{subId});");
            }
#pragma warning disable EF1002 // Risk of vulnerability to SQL injection.
            await context.Database.ExecuteSqlRawAsync(sb.ToString(), cancel);
#pragma warning restore EF1002 // Risk of vulnerability to SQL injection.
        }
    }

    /// <summary>
    /// Gets the IDs of the <c>rdf:type</c> and <c>rdfs:subClassOf</c>
    /// properties, adding them if not present.
    /// </summary>
    /// <param name="context">The context. When nodes must be added, this
    /// should have an active transaction.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Tuple with IDs.</returns>
    protected async Task<(int A, int Sub)> GetASubIdsAsync(
        CadmusGraphDbContext context, CancellationToken cancel)
    {
        if (_aSubIds.HasValue) return _aSubIds.Value;

        Dictionary<string, int> ids = await ResolveUrisAsync(context,
            ["rdf:type", "rdfs:subClassOf"], cancel);
        int a = ids["rdf:type"], sub = ids["rdfs:subClassOf"];

        int found = await context.Nodes.CountAsync(
            n => n.Id == a || n.Id == sub, cancel);
        if (found == 2)
        {
            // cache only when existing, i.e. not added by a transaction
            // which might be rolled back
            _aSubIds = (a, sub);
        }
        else
        {
            await InsertMissingNodesAsync(context,
            [
                new Node { Id = a, Label = "is-a", Tag = Node.TAG_PROPERTY },
                new Node
                {
                    Id = sub,
                    Label = "is-subclass-of",
                    Tag = Node.TAG_PROPERTY
                }
            ], cancel);
        }
        return (a, sub);
    }

    /// <summary>
    /// Collects the changes affecting node classes.
    /// </summary>
    private sealed class ClassChanges
    {
        /// <summary>
        /// The instance nodes whose classes changed (subjects of added or
        /// deleted <c>rdf:type</c> triples).
        /// </summary>
        public HashSet<int> Instances { get; } = [];

        /// <summary>
        /// The class nodes whose superclasses changed (subjects of added or
        /// deleted <c>rdfs:subClassOf</c> triples). All the instances of
        /// these classes must be updated.
        /// </summary>
        public HashSet<int> Classes { get; } = [];

        public void Add(Triple triple, int aId, int subId)
        {
            if (triple.PredicateId == aId) Instances.Add(triple.SubjectId);
            else if (triple.PredicateId == subId) Classes.Add(triple.SubjectId);
        }
    }

    private async Task UpdateClassesAsync(CadmusGraphDbContext context,
        ClassChanges changes, int aId, int subId, CancellationToken cancel)
    {
        HashSet<int> ids = [.. changes.Instances];
        if (changes.Classes.Count > 0)
        {
            List<int> classIds = [.. changes.Classes];
            ids.UnionWith(await context.NodeClasses.AsNoTracking()
                .Where(c => classIds.Contains(c.ClassId))
                .Select(c => c.NodeId)
                .Distinct()
                .ToListAsync(cancel));
        }
        if (ids.Count > 0)
            await PopulateNodeClassesAsync(context, ids, aId, subId, cancel);
    }

    private static async Task<T> RunInTransactionAsync<T>(
        CadmusGraphDbContext context, Func<Task<T>> action,
        CancellationToken cancel)
    {
        await using IDbContextTransaction tr =
            await context.Database.BeginTransactionAsync(cancel);
        T result = await action();
        await tr.CommitAsync(cancel);
        return result;
    }

    private static Task RunInTransactionAsync(CadmusGraphDbContext context,
        Func<Task> action, CancellationToken cancel) =>
        RunInTransactionAsync(context, async () =>
        {
            await action();
            return true;
        }, cancel);
    #endregion

    #region Namespace Lookup
    /// <summary>
    /// Gets the specified page of namespaces with their prefixes.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();

        IQueryable<EfNamespaceEntry> lookups =
            context.NamespaceEntries.AsNoTracking();

        if (!string.IsNullOrEmpty(filter.Prefix))
        {
            string pattern = GetContainsPattern(filter.Prefix);
            lookups = lookups.Where(l =>
                EF.Functions.Like(l.Id.ToLower(), pattern, "\\"));
        }
        if (!string.IsNullOrEmpty(filter.Uri))
        {
            string pattern = GetContainsPattern(filter.Uri);
            lookups = lookups.Where(l =>
                EF.Functions.Like(l.Uri.ToLower(), pattern, "\\"));
        }

        // get total and ret if zero
        int total = lookups.Count();
        if (total == 0)
        {
            return new DataPage<NamespaceEntry>(
                filter.PageNumber, filter.PageSize, 0, []);
        }

        List<NamespaceEntry> results = [.. lookups
            .OrderBy(l => l.Id.ToLower())
            .Skip(filter.GetSkipCount())
            .Take(filter.PageSize)
            .Select(l => l.ToNamespaceEntry())];

        return new DataPage<NamespaceEntry>(filter.PageNumber,
            filter.PageSize, total, results);
    }

    /// <summary>
    /// Looks up the namespace from its prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <returns>The namespace, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">prefix</exception>
    public string? LookupNamespace(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        using CadmusGraphDbContext context = GetContext();
        string lower = prefix.ToLowerInvariant();
        return context.NamespaceEntries.AsNoTracking()
            .Where(l => l.Id.ToLower() == lower)
            .Select(l => l.Uri)
            .FirstOrDefault();
    }

    /// <summary>
    /// Adds or updates the specified namespace prefix. If the prefix
    /// already exists (case-insensitively), its URI is replaced.
    /// </summary>
    /// <param name="prefix">The namespace prefix.</param>
    /// <param name="uri">The namespace URI corresponding to
    /// <paramref name="prefix" />.</param>
    /// <exception cref="ArgumentNullException">prefix or uri</exception>
    public void AddNamespace(string prefix, string uri)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        ArgumentNullException.ThrowIfNull(uri);

        using CadmusGraphDbContext context = GetContext();
        string lower = prefix.ToLowerInvariant();
        EfNamespaceEntry? old = context.NamespaceEntries
            .FirstOrDefault(l => l.Id.ToLower() == lower);
        if (old == null)
        {
            context.NamespaceEntries.Add(new EfNamespaceEntry
            {
                Id = prefix,
                Uri = uri
            });
        }
        else
        {
            old.Uri = uri;
        }
        context.SaveChanges();
    }

    /// <summary>
    /// Deletes a namespace by prefix.
    /// </summary>
    /// <param name="prefix">The prefix.</param>
    /// <exception cref="ArgumentNullException">prefix</exception>
    public void DeleteNamespaceByPrefix(string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);

        using CadmusGraphDbContext context = GetContext();
        string lower = prefix.ToLowerInvariant();
        context.NamespaceEntries.Where(l => l.Id.ToLower() == lower)
            .ExecuteDelete();
    }

    /// <summary>
    /// Deletes the specified namespace with all its prefixes.
    /// </summary>
    /// <param name="uri">The namespace URI.</param>
    /// <exception cref="ArgumentNullException">uri</exception>
    public void DeleteNamespaceByUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using CadmusGraphDbContext context = GetContext();
        string lower = uri.ToLowerInvariant();
        context.NamespaceEntries.Where(l => l.Uri.ToLower() == lower)
            .ExecuteDelete();
    }
    #endregion

    #region UID Lookup
    private static async Task<List<UidEntry>> GetUidEntriesAsync(
        CadmusGraphDbContext context, string unsuffixed,
        CancellationToken cancel)
    {
        string lower = unsuffixed.ToLowerInvariant();
        return await context.UidEntries.AsNoTracking()
            .Where(e => e.Unsuffixed.ToLower() == lower)
            .OrderBy(e => e.Id)
            .Select(e => new UidEntry
            {
                Id = e.Id,
                Sid = e.Sid,
                Unsuffixed = e.Unsuffixed,
                HasSuffix = e.HasSuffix
            })
            .ToListAsync(cancel);
    }

    /// <summary>
    /// Builds the UID for the specified source, eventually adding it to
    /// the UIDs lookup with a suffix. The rules are documented in
    /// <see cref="UidResolver.TryResolve"/>: in short, the same source
    /// (SID) always gets the same UID, and a numeric suffix is added only
    /// when a unique UID is requested and the UID is already used by
    /// another source.
    /// </summary>
    /// <param name="unsuffixed">The UID as calculated from its source,
    /// without any suffix. If the caller wants a unique new UID for it, it
    /// must be suffixed with <c>##</c>: this suffix will be either removed
    /// when no entry is present with the same <paramref name="unsuffixed"/>
    /// value, or replaced by a numeric suffix preceded by <c>#</c> to grant
    /// its uniqueness.</param>
    /// <param name="sid">The SID identifying the source for this UID.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The UID, eventually suffixed.</returns>
    /// <exception cref="ArgumentNullException">unsuffixed or sid</exception>
    public async Task<string> BuildUidAsync(string unsuffixed, string sid,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(unsuffixed);
        ArgumentNullException.ThrowIfNull(sid);

        (string uid, bool unique) = UidResolver.Parse(unsuffixed);
        sid = Truncate(sid, MAX_SID)!;

        await using CadmusGraphDbContext context = GetContext();

        // fast path: no insertion required
        string? resolved = UidResolver.TryResolve(
            await GetUidEntriesAsync(context, uid, cancel), uid, sid, unique,
            out _);
        if (resolved != null) return resolved;

        // else insert, serializing concurrent insertions of the same UID
        return await RunInTransactionAsync(context, async () =>
        {
            await LockAsync(context, LOCK_UID, [uid], cancel);

            // check again, as others might have inserted it meanwhile
            resolved = UidResolver.TryResolve(
                await GetUidEntriesAsync(context, uid, cancel), uid, sid,
                unique, out bool hasSuffix);
            if (resolved != null) return resolved;

            EfUidEntry entry = new()
            {
                Sid = sid,
                Unsuffixed = uid,
                HasSuffix = hasSuffix
            };
            context.UidEntries.Add(entry);
            await context.SaveChangesAsync(cancel);
            return UidResolver.BuildUid(uid, entry.Id, hasSuffix);
        }, cancel);
    }

    /// <summary>
    /// Builds the UID for the specified source (see
    /// <see cref="BuildUidAsync"/>).
    /// </summary>
    /// <param name="unsuffixed">The UID as calculated from its source,
    /// without any suffix, optionally ending with <c>##</c> to request
    /// a unique UID.</param>
    /// <param name="sid">The SID identifying the source for this UID.</param>
    /// <returns>The UID, eventually suffixed.</returns>
    /// <exception cref="ArgumentNullException">unsuffixed or sid</exception>
    public string BuildUid(string unsuffixed, string sid) =>
        BuildUidAsync(unsuffixed, sid).GetAwaiter().GetResult();
    #endregion

    #region URI Lookup
    private static async Task LoadUrisAsync(CadmusGraphDbContext context,
        IList<string> lowerUris, Dictionary<string, int> target,
        CancellationToken cancel)
    {
        foreach (var entry in await context.UriEntries.AsNoTracking()
            .Where(e => lowerUris.Contains(e.Uri.ToLower()))
            // older databases might have case-variant duplicates: the
            // oldest one wins, as in single lookups
            .OrderBy(e => e.Id)
            .Select(e => new { e.Id, e.Uri })
            .ToListAsync(cancel))
        {
            target.TryAdd(entry.Uri, entry.Id);
        }
    }

    /// <summary>
    /// Resolves the specified URIs into their numeric IDs, adding to the
    /// lookup those which are missing. URIs are case-insensitive.
    /// When adding URIs, the context should have an active transaction,
    /// so that concurrent insertions can be serialized.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="uris">The URIs.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Case-insensitive dictionary with the ID of each URI.
    /// </returns>
    /// <exception cref="CadmusGraphException">empty or too long URI
    /// </exception>
    protected async Task<Dictionary<string, int>> ResolveUrisAsync(
        CadmusGraphDbContext context, IEnumerable<string> uris,
        CancellationToken cancel)
    {
        Dictionary<string, int> ids = new(StringComparer.OrdinalIgnoreCase);
        List<string> distinct = [.. uris.Distinct(
            StringComparer.OrdinalIgnoreCase)];
        if (distinct.Count == 0) return ids;

        foreach (string uri in distinct)
        {
            if (string.IsNullOrWhiteSpace(uri))
                throw new CadmusGraphException("Empty URI");
            if (uri.Length > MAX_URI)
            {
                throw new CadmusGraphException(
                    $"URI longer than {MAX_URI} characters: {uri}");
            }
        }

        await LoadUrisAsync(context,
            [.. distinct.Select(u => u.ToLowerInvariant())], ids, cancel);
        List<string> missing = [.. distinct.Where(u => !ids.ContainsKey(u))];
        if (missing.Count == 0) return ids;

        // serialize insertions of the same URIs, then check again
        await LockAsync(context, LOCK_URI, missing, cancel);
        await LoadUrisAsync(context,
            [.. missing.Select(u => u.ToLowerInvariant())], ids, cancel);
        missing = [.. missing.Where(u => !ids.ContainsKey(u))];
        if (missing.Count == 0) return ids;

        List<EfUriEntry> entries = [.. missing.Select(
            u => new EfUriEntry { Uri = u })];
        context.UriEntries.AddRange(entries);
        await context.SaveChangesAsync(cancel);
        foreach (EfUriEntry entry in entries) ids[entry.Uri] = entry.Id;

        return ids;
    }

    /// <summary>
    /// Adds the specified URI in the mapped URIs set.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>ID assigned to the URI.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public int AddUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using CadmusGraphDbContext context = GetContext();
        return RunInTransactionAsync(context, async () =>
            (await ResolveUrisAsync(context, [uri], default))[uri],
            default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Lookups the URI from its numeric ID.
    /// </summary>
    /// <param name="id">The numeric ID for the URI.</param>
    /// <returns>The URI, or null if not found.</returns>
    public string? LookupUri(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        return context.UriEntries.AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => l.Uri)
            .FirstOrDefault();
    }

    /// <summary>
    /// Lookups the numeric ID from its URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The ID, or 0 if not found.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public int LookupId(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using CadmusGraphDbContext context = GetContext();
        string lower = uri.ToLowerInvariant();
        return context.UriEntries.AsNoTracking()
            .Where(l => l.Uri.ToLower() == lower)
            .OrderBy(l => l.Id)
            .Select(l => l.Id)
            .FirstOrDefault();
    }
    #endregion

    #region Node
    private static IQueryable<EfNode> ApplyNodeFilterBase(
        IQueryable<EfNode> nodes, NodeFilterBase filter)
    {
        // uid
        if (!string.IsNullOrEmpty(filter.Uid))
        {
            string pattern = GetContainsPattern(filter.Uid);
            nodes = nodes.Where(n => EF.Functions.Like(
                n.UriEntry!.Uri.ToLower(), pattern, "\\"));
        }

        // class
        if (filter.IsClass != null)
            nodes = nodes.Where(n => n.IsClass == filter.IsClass.Value);

        // tag
        if (filter.Tag != null)
        {
            string tag = filter.Tag.ToLowerInvariant();
            nodes = filter.Tag.Length == 0
                ? nodes.Where(n => n.Tag == null)
                : nodes.Where(n => n.Tag!.ToLower() == tag);
        }

        // label
        if (!string.IsNullOrEmpty(filter.Label))
        {
            string pattern = GetContainsPattern(filter.Label);
            nodes = nodes.Where(n => n.Label != null &&
                EF.Functions.Like(n.Label.ToLower(), pattern, "\\"));
        }

        // source type
        if (filter.SourceType != null)
            nodes = nodes.Where(n => n.SourceType == filter.SourceType.Value);

        // sid
        if (!string.IsNullOrEmpty(filter.Sid))
        {
            string sid = filter.Sid.ToLowerInvariant();
            nodes = filter.IsSidPrefix
                ? nodes.Where(n => n.Sid != null &&
                    n.Sid.ToLower().StartsWith(sid))
                : nodes.Where(n => n.Sid != null && n.Sid.ToLower() == sid);
        }

        // class IDs: nodes with any of the specified class IDs
        if (filter.ClassIds?.Count > 0)
        {
            List<int> classIds = filter.ClassIds;
            nodes = nodes.Where(n =>
                n.Classes!.Any(c => classIds.Contains(c.ClassId)));
        }

        return nodes;
    }

    /// <summary>
    /// Gets the requested page of nodes.
    /// </summary>
    /// <param name="filter">The nodes filter.</param>
    /// <returns>The page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriNode> GetNodes(NodeFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfNode> nodes = ApplyNodeFilterBase(
            context.Nodes.AsNoTracking(), filter);

        // additional filter: linked node ID
        if (filter.LinkedNodeId > 0)
        {
            int linkedId = filter.LinkedNodeId;
            nodes = char.ToUpperInvariant(filter.LinkedNodeRole) switch
            {
                // nodes being objects of a triple whose subject is the
                // linked node
                'S' => nodes.Where(n => n.ObjectTriples!.Any(
                    t => t.SubjectId == linkedId)),

                // nodes being subjects of a triple whose object is the
                // linked node
                'O' => nodes.Where(n => n.SubjectTriples!.Any(
                    t => t.ObjectId == linkedId)),

                // nodes linked to the linked node as either S or O
                _ => nodes.Where(n =>
                    n.ObjectTriples!.Any(t => t.SubjectId == linkedId) ||
                    n.SubjectTriples!.Any(t => t.ObjectId == linkedId)),
            };
        }

        int total = nodes.Count();
        if (total == 0)
        {
            return new DataPage<UriNode>(filter.PageNumber, filter.PageSize, 0,
                []);
        }

        List<UriNode> results = [.. nodes
            .OrderBy(n => n.Label!.ToLower())
            .ThenBy(n => n.Id)
            .Skip(filter.GetSkipCount())
            .Take(filter.PageSize)
            .Select(n => new UriNode
            {
                Id = n.Id,
                IsClass = n.IsClass,
                Tag = n.Tag,
                Label = n.Label,
                SourceType = n.SourceType,
                Sid = n.Sid,
                Uri = n.UriEntry!.Uri
            })];
        return new DataPage<UriNode>(filter.PageNumber, filter.PageSize, total,
            results);
    }

    /// <summary>
    /// Gets all the nodes with the specified IDs.
    /// </summary>
    /// <param name="ids">The nodes IDs.</param>
    /// <returns>List of nodes (or null), one per ID.</returns>
    /// <exception cref="ArgumentNullException">ids</exception>
    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        if (ids.Count == 0) return [];

        using CadmusGraphDbContext context = GetContext();
        List<int> distinct = [.. ids.Distinct()];
        Dictionary<int, UriNode> nodes = context.Nodes
            .AsNoTracking()
            .Include(n => n.UriEntry)
            .Where(n => distinct.Contains(n.Id))
            .AsEnumerable()
            .ToDictionary(n => n.Id, n => n.ToUriNode());

        return [.. ids.Select(id => nodes.GetValueOrDefault(id))];
    }

    /// <summary>
    /// Gets the node with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The node or null if not found.</returns>
    public UriNode? GetNode(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfNode? node = context.Nodes
            .AsNoTracking()
            .Include(n => n.UriEntry)
            .FirstOrDefault(n => n.Id == id);
        return node?.ToUriNode();
    }

    /// <summary>
    /// Gets the node by its URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The node or null if not found.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public UriNode? GetNodeByUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using CadmusGraphDbContext context = GetContext();
        string lower = uri.ToLowerInvariant();
        EfNode? node = context.Nodes
            .AsNoTracking()
            .Include(n => n.UriEntry)
            .Where(n => n.UriEntry!.Uri.ToLower() == lower)
            .OrderBy(n => n.Id)
            .FirstOrDefault();
        return node?.ToUriNode();
    }

    private static void CopyNode(Node source, EfNode target)
    {
        target.IsClass = source.IsClass;
        target.Tag = Truncate(source.Tag, MAX_TAG);
        target.Label = Truncate(source.Label, MAX_LABEL);
        target.SourceType = source.SourceType;
        target.Sid = Truncate(source.Sid, MAX_SID);
    }

    /// <summary>
    /// Adds or updates the specified node. Note that it is assumed that
    /// the node's ID has been set, either because the node already exists,
    /// or because its ID has been calculated with <see cref="AddUri(string)"/>.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <param name="noUpdate">True to avoid updating an existing node.
    /// When this is true, the node is added when not existing; when
    /// existing, nothing is done.</param>
    /// <exception cref="ArgumentNullException">node</exception>
    public void AddNode(Node node, bool noUpdate = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            EfNode? old = await context.Nodes.FindAsync(node.Id);
            if (old != null)
            {
                if (noUpdate) return;
                CopyNode(node, old);
                await context.SaveChangesAsync();
            }
            else
            {
                await InsertMissingNodesAsync(context, [node], default);
            }

            (int aId, int subId) = await GetASubIdsAsync(context, default);
            await PopulateNodeClassesAsync(context, [node.Id], aId, subId,
                default);
        }, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Bulk imports the specified nodes. Note that here the nodes being
    /// imported are assumed to have a URI, while their ID will be
    /// calculated during import according to the URI value. Nodes without
    /// URI will not be imported. Existing nodes are updated.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <exception cref="ArgumentNullException">nodes</exception>
    public void ImportNodes(IEnumerable<UriNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        List<UriNode> list = [.. nodes.Where(n => !string.IsNullOrEmpty(n.Uri))];
        if (list.Count == 0) return;

        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            Dictionary<string, int> ids = await ResolveUrisAsync(context,
                list.Select(n => n.Uri!), default);
            foreach (UriNode node in list) node.Id = ids[node.Uri!];

            List<int> nodeIds = [.. ids.Values];
            Dictionary<int, EfNode> existing = await context.Nodes
                .Where(n => nodeIds.Contains(n.Id))
                .ToDictionaryAsync(n => n.Id);

            List<Node> missing = [];
            foreach (UriNode node in list)
            {
                if (existing.TryGetValue(node.Id, out EfNode? old))
                    CopyNode(node, old);
                else
                    missing.Add(node);
            }
            await context.SaveChangesAsync();
            await InsertMissingNodesAsync(context,
                [.. missing.OrderBy(n => n.Id)], default);

            (int aId, int subId) = await GetASubIdsAsync(context, default);
            await PopulateNodeClassesAsync(context, nodeIds, aId, subId,
                default);
        }, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes the node with the specified ID. This also deletes all the
    /// triples using it.
    /// </summary>
    /// <param name="id">The node identifier.</param>
    public void DeleteNode(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            EfNode? node = await context.Nodes.FindAsync(id);
            if (node == null) return;

            (int aId, int subId) = await GetASubIdsAsync(context, default);

            // nodes whose classes will change: instances of the node
            // (when it's a class), either direct (S a node) or inherited
            HashSet<int> affected = [.. await context.NodeClasses
                .Where(c => c.ClassId == id)
                .Select(c => c.NodeId)
                .ToListAsync()];
            affected.Remove(id);

            context.Nodes.Remove(node);
            await context.SaveChangesAsync();

            if (affected.Count > 0)
            {
                await PopulateNodeClassesAsync(context, affected, aId, subId,
                    default);
            }
        }, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the nodes included in a triple with the specified predicate ID
    /// and other node ID, either as its subject or as its object.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriNode> GetLinkedNodes(LinkedNodeFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfNode> nodes = context.Nodes.AsNoTracking();
        int pid = filter.PredicateId, otherId = filter.OtherNodeId;

        nodes = filter.IsObject
            // nodes being objects of triples with the specified predicate
            // having the specified other node as subject
            ? nodes.Where(n => n.ObjectTriples!.Any(
                t => t.PredicateId == pid && t.SubjectId == otherId))
            // nodes being subjects of triples with the specified predicate
            // having the specified other node as object
            : nodes.Where(n => n.SubjectTriples!.Any(
                t => t.PredicateId == pid && t.ObjectId == otherId));

        nodes = ApplyNodeFilterBase(nodes, filter);

        // get total and ret if zero
        int total = nodes.Count();
        if (total == 0)
        {
            return new DataPage<UriNode>(
                filter.PageNumber, filter.PageSize, 0, []);
        }

        List<UriNode> results = [.. nodes
            .OrderBy(n => n.Label!.ToLower())
            .ThenBy(n => n.UriEntry!.Uri.ToLower())
            .ThenBy(n => n.Id)
            .Skip(filter.GetSkipCount())
            .Take(filter.PageSize)
            .Select(n => new UriNode
            {
                Id = n.Id,
                IsClass = n.IsClass,
                Tag = n.Tag,
                Label = n.Label,
                SourceType = n.SourceType,
                Sid = n.Sid,
                Uri = n.UriEntry!.Uri
            })];
        return new DataPage<UriNode>(filter.PageNumber, filter.PageSize, total,
            results);
    }

    /// <summary>
    /// Builds the regex match expression for field value vs pattern.
    /// This is no longer used, as all the regular expressions are now
    /// passed as SQL parameters (see <see cref="GetRegexMatchSql"/>).
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="pattern">The regex pattern.</param>
    /// <returns>SQL code.</returns>
    protected abstract string BuildRegexMatch(string field, string pattern);

    /// <summary>
    /// Builds the SQL code for a regular expression match between a field
    /// and a parameter in the form <c>{N}</c>, as used by raw SQL methods
    /// of Entity Framework. The default implementation uses the
    /// PostgreSQL <c>~</c> operator.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="parameterIndex">The index of the parameter.</param>
    /// <returns>SQL code.</returns>
    protected virtual string GetRegexMatchSql(string field, int parameterIndex)
        => $"{field} ~ {{{parameterIndex}}}";

    /// <summary>
    /// Builds the SQL code with regular expressions matches for the
    /// specified fields and patterns.
    /// </summary>
    /// <param name="fieldAndPatterns">The fields and patterns.</param>
    /// <returns>SQL code.</returns>
    [Obsolete("Concatenates patterns in SQL: use parameters instead")]
    protected string BuildRawRegexSql(
        IList<Tuple<string, string?>> fieldAndPatterns)
    {
        ArgumentNullException.ThrowIfNull(fieldAndPatterns);

        StringBuilder sb = new();
        int n = 0;
        foreach (Tuple<string, string?> fp in fieldAndPatterns
            .Where(t => t.Item2 != null))
        {
            if (++n > 1) sb.AppendLine(" AND");
            sb.AppendLine(BuildRegexMatch(fp.Item1, fp.Item2!));
        }
        return sb.ToString();
    }

    private IQueryable<EfTriple> GetFilteredTriples(LiteralFilter filter,
        CadmusGraphDbContext context)
    {
        // literal pattern
        IQueryable<EfTriple> triples = !string.IsNullOrEmpty(filter.LiteralPattern)
#pragma warning disable EF1003 // only the field name is concatenated
            ? context.Triples.FromSqlRaw(
                "SELECT * FROM triple WHERE " + GetRegexMatchSql("o_lit", 0),
                filter.LiteralPattern)
#pragma warning restore EF1003
            : context.Triples;

        triples = triples.Include(t => t.Subject).ThenInclude(n => n!.UriEntry)
                         .Include(t => t.Predicate).ThenInclude(n => n!.UriEntry)
                         .Include(t => t.Object).ThenInclude(n => n!.UriEntry);

        // literal type
        if (!string.IsNullOrEmpty(filter.LiteralType))
        {
            string type = filter.LiteralType.ToLowerInvariant();
            triples = triples.Where(t => t.LiteralType!.ToLower() == type);
        }

        // literal language
        if (!string.IsNullOrEmpty(filter.LiteralLanguage))
        {
            string lang = filter.LiteralLanguage.ToLowerInvariant();
            triples = triples.Where(t => t.LiteralLanguage!.ToLower() == lang);
        }

        // min literal number
        if (filter.MinLiteralNumber.HasValue)
        {
            double min = filter.MinLiteralNumber.Value;
            triples = triples.Where(t => t.LiteralNumber != null &&
                t.LiteralNumber >= min);
        }

        // max literal number
        if (filter.MaxLiteralNumber.HasValue)
        {
            double max = filter.MaxLiteralNumber.Value;
            triples = triples.Where(t => t.LiteralNumber != null &&
                t.LiteralNumber <= max);
        }

        return triples.AsNoTracking();
    }

    private IQueryable<EfTriple> GetFilteredTriples(TripleFilter filter,
        CadmusGraphDbContext context)
    {
        IQueryable<EfTriple> triples = GetFilteredTriples((LiteralFilter)filter,
            context);

        // subject ID
        if (filter.SubjectId > 0)
            triples = triples.Where(t => t.SubjectId == filter.SubjectId);

        // predicate ID
        if (filter.PredicateIds?.Count > 0)
        {
            List<int> ids = [.. filter.PredicateIds];
            triples = triples.Where(t => ids.Contains(t.PredicateId));
        }
        if (filter.NotPredicateIds?.Count > 0)
        {
            List<int> ids = [.. filter.NotPredicateIds];
            triples = triples.Where(t => !ids.Contains(t.PredicateId));
        }

        // has literal and object ID
        if (filter.HasLiteralObject != null)
        {
            triples = filter.HasLiteralObject.Value
                ? triples.Where(t => t.ObjectId == null)
                : triples.Where(t => t.ObjectId != null);
        }
        if (filter.ObjectId > 0)
            triples = triples.Where(t => t.ObjectId == filter.ObjectId);

        // sid
        if (!string.IsNullOrEmpty(filter.Sid))
        {
            string sid = filter.Sid.ToLowerInvariant();
            triples = filter.IsSidPrefix
                ? triples.Where(t => t.Sid!.ToLower().StartsWith(sid))
                : triples.Where(t => t.Sid!.ToLower() == sid);
        }

        // tag (null if empty)
        if (filter.Tag != null)
        {
            string tag = filter.Tag.ToLowerInvariant();
            triples = filter.Tag.Length == 0
                ? triples.Where(t => t.Tag == null)
                : triples.Where(t => t.Tag!.ToLower() == tag);
        }

        return triples;
    }

    /// <summary>
    /// Gets the literals included in a triple with the specified subject ID
    /// and predicate ID.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriTriple> GetLinkedLiterals(LinkedLiteralFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();

        IQueryable<EfTriple> triples = GetFilteredTriples(filter, context)
            .Where(t => t.SubjectId == filter.SubjectId &&
                t.PredicateId == filter.PredicateId &&
                t.ObjectId == null);

        // get total and return if zero
        int total = triples.Count();
        if (total == 0)
        {
            return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
                0, []);
        }

        List<EfTriple> results = [.. triples
            .OrderBy(t => t.ObjectLiteralIx!.ToLower())
            .ThenBy(t => t.Id)
            .Skip(filter.GetSkipCount())
            .Take(filter.PageSize)];
        return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize, total,
            results.ConvertAll(t => t.ToUriTriple()));
    }
    #endregion

    #region Property
    /// <summary>
    /// Gets the specified page of properties.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriProperty> GetProperties(PropertyFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfProperty> properties = context.Properties
            .AsNoTracking()
            .Include(p => p.Node)
            .ThenInclude(n => n!.UriEntry);

        if (!string.IsNullOrEmpty(filter.Uid))
        {
            string pattern = GetContainsPattern(filter.Uid);
            properties = properties.Where(p => EF.Functions.Like(
                p.Node!.UriEntry!.Uri.ToLower(), pattern, "\\"));
        }

        if (!string.IsNullOrEmpty(filter.DataType))
        {
            string type = filter.DataType.ToLowerInvariant();
            properties = properties.Where(p => p.DataType!.ToLower() == type);
        }
        if (!string.IsNullOrEmpty(filter.LiteralEditor))
        {
            string editor = filter.LiteralEditor.ToLowerInvariant();
            properties = properties.Where(p =>
                p.LitEditor!.ToLower() == editor);
        }

        // get total and return if zero
        int total = properties.Count();
        if (total == 0)
        {
            return new DataPage<UriProperty>(filter.PageNumber, filter.PageSize,
                0, []);
        }

        List<EfProperty> results = [.. properties
            .OrderBy(p => p.Node!.UriEntry!.Uri.ToLower())
            .ThenBy(p => p.Id)
            .Skip(filter.GetSkipCount())
            .Take(filter.PageSize)];

        return new DataPage<UriProperty>(filter.PageNumber, filter.PageSize,
            total, results.ConvertAll(p => p.ToUriProperty()));
    }

    /// <summary>
    /// Gets the property with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <returns>The property or null if not found.</returns>
    public UriProperty? GetProperty(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfProperty? property = context.Properties
            .AsNoTracking()
            .Include(p => p.Node)
            .ThenInclude(n => n!.UriEntry)
            .FirstOrDefault(p => p.Id == id);
        return property?.ToUriProperty();
    }

    /// <summary>
    /// Gets the property by its URI.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The property or null if not found.</returns>
    /// <exception cref="ArgumentNullException">uri</exception>
    public UriProperty? GetPropertyByUri(string uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        using CadmusGraphDbContext context = GetContext();
        string lower = uri.ToLowerInvariant();
        EfProperty? property = context.Properties
            .AsNoTracking()
            .Include(p => p.Node)
            .ThenInclude(n => n!.UriEntry)
            .FirstOrDefault(p => p.Node!.UriEntry!.Uri.ToLower() == lower);
        return property?.ToUriProperty();
    }

    /// <summary>
    /// Adds or updates the specified property. Its node must exist.
    /// </summary>
    /// <param name="property">The property.</param>
    /// <exception cref="ArgumentNullException">property</exception>
    public void AddProperty(Property property)
    {
        ArgumentNullException.ThrowIfNull(property);

        using CadmusGraphDbContext context = GetContext();
        EfProperty? old = context.Properties.Find(property.Id);
        if (old != null)
        {
            old.DataType = property.DataType;
            old.LitEditor = property.LiteralEditor;
            old.Description = property.Description;
        }
        else
        {
            context.Properties.Add(new EfProperty(property));
        }
        context.SaveChanges();
    }

    /// <summary>
    /// Deletes the property with the specified ID.
    /// </summary>
    /// <param name="id">The property identifier.</param>
    public void DeleteProperty(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        context.Properties.Where(p => p.Id == id).ExecuteDelete();
    }
    #endregion

    #region Triples
    /// <summary>
    /// Gets the specified page of triples.
    /// </summary>
    /// <param name="filter">The filter. You can set the page size to 0
    /// to get all the matches at once.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public DataPage<UriTriple> GetTriples(TripleFilter filter)
    {
        ArgumentNullException.ThrowIfNull(filter);

        using CadmusGraphDbContext context = GetContext();
        IQueryable<EfTriple> triples = GetFilteredTriples(filter, context);

        // get total and return if zero
        int total = triples.Count();
        if (total == 0)
        {
            return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
                0, []);
        }

        triples = triples.OrderBy(t => t.Subject!.UriEntry!.Uri.ToLower())
            .ThenBy(t => t.Predicate!.UriEntry!.Uri.ToLower())
            .ThenBy(t => t.Id)
            .Skip(filter.GetSkipCount());

        if (filter.PageSize > 0) triples = triples.Take(filter.PageSize);

        return new DataPage<UriTriple>(filter.PageNumber, filter.PageSize,
            total, [.. triples.AsEnumerable().Select(t => t.ToUriTriple())]);
    }

    /// <summary>
    /// Gets the triple with the specified ID.
    /// </summary>
    /// <param name="id">The triple's ID.</param>
    public UriTriple? GetTriple(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        EfTriple? triple = context.Triples
            .AsNoTracking()
            .Include(t => t.Subject)
            .ThenInclude(n => n!.UriEntry)
            .Include(t => t.Predicate)
            .ThenInclude(n => n!.UriEntry)
            .Include(t => t.Object)
            .ThenInclude(n => n!.UriEntry)
            .FirstOrDefault(t => t.Id == id);
        return triple?.ToUriTriple();
    }

    private static Task<EfTriple?> FindTripleByValueAsync(Triple triple,
        CadmusGraphDbContext context, CancellationToken cancel)
    {
        string? tag = triple.Tag?.ToLowerInvariant();

        IQueryable<EfTriple> triples = context.Triples
            .Where(t => t.SubjectId == triple.SubjectId &&
                        t.PredicateId == triple.PredicateId &&
                        t.Sid == triple.Sid &&
                        (tag == null ? t.Tag == null : t.Tag!.ToLower() == tag));

        triples = triple.ObjectId != null && triple.ObjectId > 0
            ? triples.Where(t => t.ObjectId == triple.ObjectId)
            : triples.Where(t => t.ObjectId == null &&
                // literals must match for casing too
                t.ObjectLiteral == triple.ObjectLiteral &&
                t.LiteralType == triple.LiteralType &&
                t.LiteralLanguage == triple.LiteralLanguage);

        return triples.OrderBy(t => t.Id).FirstOrDefaultAsync(cancel);
    }

    private static void CopyTriple(Triple source, EfTriple target)
    {
        target.SubjectId = source.SubjectId;
        target.PredicateId = source.PredicateId;
        target.ObjectId = source.ObjectId == 0 ? null : source.ObjectId;
        target.ObjectLiteral = source.ObjectLiteral;
        CopyTripleDetails(source, target);
    }

    private static void CopyTripleDetails(Triple source, EfTriple target)
    {
        target.ObjectLiteralIx = Truncate(source.ObjectLiteralIx, MAX_LIT_IX);
        target.LiteralType = Truncate(source.LiteralType, MAX_LIT_TYPE);
        target.LiteralLanguage = Truncate(source.LiteralLanguage, MAX_LIT_LANG);
        target.LiteralNumber = source.LiteralNumber;
        target.Sid = Truncate(source.Sid, MAX_SID);
        target.Tag = Truncate(source.Tag, MAX_TAG);
    }

    private static EfTriple CreateTriple(Triple source)
    {
        EfTriple triple = new() { Id = 0 };
        CopyTriple(source, triple);
        return triple;
    }

    /// <summary>
    /// Adds or updates the specified triple. If the triple is new (ID=0)
    /// and a triple with all the same values already exists, nothing is
    /// done.
    /// When <paramref name="triple"/> has ID=0 (=new triple), its
    /// <see cref="Triple.Id"/> property gets updated by this method
    /// after insertion.
    /// </summary>
    /// <param name="triple">The triple.</param>
    /// <exception cref="ArgumentNullException">triple</exception>
    public void AddTriple(Triple triple)
    {
        ArgumentNullException.ThrowIfNull(triple);

        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            // nope if exactly the same triple already exists.
            // In this case, update the triple's ID to ensure it's valid
            EfTriple? same = await FindTripleByValueAsync(triple, context,
                default);
            if (same != null)
            {
                triple.Id = same.Id;
                return;
            }

            (int aId, int subId) = await GetASubIdsAsync(context, default);
            ClassChanges changes = new();

            EfTriple? old = triple.Id == 0
                ? null : await context.Triples.FindAsync(triple.Id);
            if (old == null)
            {
                EfTriple added = CreateTriple(triple);
                context.Triples.Add(added);
                await context.SaveChangesAsync();
                triple.Id = added.Id;
            }
            else
            {
                changes.Add(old, aId, subId);
                CopyTriple(triple, old);
                await context.SaveChangesAsync();
            }
            changes.Add(triple, aId, subId);
            await UpdateClassesAsync(context, changes, aId, subId, default);
        }, default).GetAwaiter().GetResult();
    }

    private sealed record ImportTripleKey(int S, int P, int O, string? Lit,
        string? Type, string? Lang, string? Sid);

    private static ImportTripleKey GetImportKey(Triple t) => new(
        t.SubjectId, t.PredicateId, t.ObjectId ?? 0,
        t.ObjectId is null or 0 ? t.ObjectLiteral : null,
        t.LiteralType, t.LiteralLanguage, t.Sid);

    /// <summary>
    /// Bulk imports the specified triples. Note that it is assumed that
    /// all the triple's non-literal terms have a URI; if it is missing,
    /// the triple will not be imported. The URI is used to generate the
    /// corresponding numeric IDs used internally. Nodes are added for
    /// missing URIs. Triples equal to existing ones are not imported.
    /// </summary>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">triples</exception>
    public void ImportTriples(IEnumerable<UriTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(triples);

        List<UriTriple> list = [.. triples.Where(t =>
            t.SubjectUri != null && t.PredicateUri != null &&
            (t.ObjectLiteral != null || t.ObjectUri != null))];
        if (list.Count == 0) return;

        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            Dictionary<string, int> ids = await ResolveUrisAsync(context,
                list.SelectMany(t => t.ObjectUri != null
                    ? new[] { t.SubjectUri!, t.PredicateUri!, t.ObjectUri }
                    : [t.SubjectUri!, t.PredicateUri!]), default);

            // nodes: add the missing ones
            Dictionary<int, Node> nodes = [];
            foreach (UriTriple triple in list)
            {
                triple.SubjectId = ids[triple.SubjectUri!];
                triple.PredicateId = ids[triple.PredicateUri!];
                triple.ObjectId = triple.ObjectUri != null
                    ? ids[triple.ObjectUri] : null;

                nodes.TryAdd(triple.SubjectId, new Node
                {
                    Id = triple.SubjectId,
                    Label = triple.SubjectUri,
                    Sid = triple.Sid
                });
                Node predicate = new()
                {
                    Id = triple.PredicateId,
                    Label = triple.PredicateUri,
                    Sid = triple.Sid,
                    Tag = Node.TAG_PROPERTY
                };
                if (!nodes.TryAdd(triple.PredicateId, predicate))
                    nodes[triple.PredicateId].Tag = Node.TAG_PROPERTY;
                if (triple.ObjectId != null)
                {
                    nodes.TryAdd(triple.ObjectId.Value, new Node
                    {
                        Id = triple.ObjectId.Value,
                        Label = triple.ObjectUri,
                        Sid = triple.Sid
                    });
                }
            }
            await InsertMissingNodesAsync(context,
                [.. nodes.Values.OrderBy(n => n.Id)], default);

            // triples: skip those already existing
            List<int> subjectIds = [.. list.Select(t => t.SubjectId).Distinct()];
            HashSet<ImportTripleKey> existing = [.. (await context.Triples
                .AsNoTracking()
                .Where(t => subjectIds.Contains(t.SubjectId))
                .ToListAsync()).Select(GetImportKey)];

            (int aId, int subId) = await GetASubIdsAsync(context, default);
            ClassChanges changes = new();
            List<(UriTriple Source, EfTriple Target)> added = [];
            foreach (UriTriple triple in list)
            {
                if (!existing.Add(GetImportKey(triple))) continue;
                EfTriple target = CreateTriple(triple);
                context.Triples.Add(target);
                added.Add((triple, target));
                changes.Add(triple, aId, subId);
            }
            await context.SaveChangesAsync();
            foreach ((UriTriple source, EfTriple target) in added)
                source.Id = target.Id;

            await UpdateClassesAsync(context, changes, aId, subId, default);
        }, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Deletes the triple with the specified ID.
    /// </summary>
    /// <param name="id">The identifier.</param>
    public void DeleteTriple(int id)
    {
        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            EfTriple? triple = await context.Triples.FindAsync(id);
            if (triple == null) return;

            context.Triples.Remove(triple);
            await context.SaveChangesAsync();

            (int aId, int subId) = await GetASubIdsAsync(context, default);
            ClassChanges changes = new();
            changes.Add(triple, aId, subId);
            await UpdateClassesAsync(context, changes, aId, subId, default);
        }, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Gets the specified page of triples variously filtered, and grouped
    /// by their predicate.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="sort">The sort order: any combination of <c>c</c>=by
    /// count, ascending; <c>C</c>=by count, descending; <c>u</c>=by URI,
    /// ascending; <c>U</c>=by URI, descending.</param>
    /// <returns>Page.</returns>
    /// <exception cref="ArgumentNullException">filter or sort</exception>
    public DataPage<TripleGroup> GetTripleGroups(TripleFilter filter,
        string sort = "Cu")
    {
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(sort);
        if (sort.Length == 0) sort = "Cu";

        using CadmusGraphDbContext context = GetContext();
        // inner query:
        // select p_id, count(p_id) as cnt FROM triple
        // where ...
        // group by p_id
        IQueryable<EfTriple> triples = GetFilteredTriples(filter, context);
        IQueryable<TripleGroup> groups = triples.GroupBy(t => t.PredicateId)
            .Select(g => new TripleGroup
            {
                PredicateId = g.Key,
                Count = g.Count()
            });
        int total = groups.Count();
        if (total == 0)
        {
            return new DataPage<TripleGroup>(
                filter.PageNumber, filter.PageSize, 0, []);
        }

        // get groups PredicateUri by joining with URIs
        groups = groups.Join(context.UriEntries,
            g => g.PredicateId,
            u => u.Id,
            (g, u) => new TripleGroup
            {
                PredicateId = g.PredicateId,
                PredicateUri = u.Uri,
                Count = g.Count
            });

        // sorting
        IOrderedQueryable<TripleGroup> sortedGroups = sort[0] switch
        {
            'c' => groups.OrderBy(g => g.Count),
            'C' => groups.OrderByDescending(g => g.Count),
            'u' => groups.OrderBy(g => g.PredicateUri),
            'U' => groups.OrderByDescending(g => g.PredicateUri),
            _ => groups.OrderByDescending(g => g.Count),
        };
        for (int i = 1; i < sort.Length; i++)
        {
            sortedGroups = sort[i] switch
            {
                'c' => sortedGroups.ThenBy(g => g.Count),
                'C' => sortedGroups.ThenByDescending(g => g.Count),
                'u' => sortedGroups.ThenBy(g => g.PredicateUri),
                'U' => sortedGroups.ThenByDescending(g => g.PredicateUri),
                _ => sortedGroups.ThenByDescending(g => g.Count),
            };
        }
        sortedGroups = sortedGroups.ThenBy(g => g.PredicateId);

        List<TripleGroup> results = [.. sortedGroups
            .Skip(filter.GetSkipCount())
            .Take(filter.PageSize)];

        return new DataPage<TripleGroup>(filter.PageNumber, filter.PageSize,
            total, results);
    }
    #endregion

    #region Thesaurus
    /// <summary>
    /// Adds the specified thesaurus as a set of class nodes.
    /// </summary>
    /// <param name="thesaurus">The thesaurus.</param>
    /// <param name="includeRoot">If set to <c>true</c>, include a root node
    /// corresponding to the thesaurus ID. This typically happens for
    /// non-hierarchic thesauri, where a flat list of entries is grouped
    /// under a single root.</param>
    /// <param name="prefix">The optional prefix to prepend to each ID.</param>
    /// <exception cref="ArgumentNullException">thesaurus</exception>
    public void AddThesaurus(Thesaurus thesaurus, bool includeRoot,
        string? prefix = null)
    {
        ArgumentNullException.ThrowIfNull(thesaurus);

        // nothing to do for aliases
        if (thesaurus.TargetId != null) return;

        // collect nodes and parent-child relationships
        List<(string Uri, string Label)> nodes = [];
        List<(string Child, string Parent)> links = [];

        string? rootUri = null;
        if (includeRoot)
        {
            int atIndex = thesaurus.Id.LastIndexOf('@');
            string id = atIndex > -1 ? thesaurus.Id[..atIndex] : thesaurus.Id;
            rootUri = prefix + id;
            nodes.Add((rootUri, id));
        }

        thesaurus.VisitByLevel(entry =>
        {
            string uri = prefix + entry.Id;
            nodes.Add((uri, entry.Id));
            if (entry.Parent != null)
                links.Add((uri, prefix + entry.Parent.Id));
            else if (rootUri != null)
                links.Add((uri, rootUri));
            return true;
        });

        using CadmusGraphDbContext context = GetContext();
        RunInTransactionAsync(context, async () =>
        {
            (int aId, int subId) = await GetASubIdsAsync(context, default);
            Dictionary<string, int> ids = await ResolveUrisAsync(context,
                nodes.Select(n => n.Uri), default);

            await InsertMissingNodesAsync(context, [.. nodes
                .Select(n => new Node
                {
                    Id = ids[n.Uri],
                    IsClass = true,
                    Label = n.Label,
                    SourceType = Node.SOURCE_THESAURUS,
                    Tag = "thesaurus",
                    Sid = thesaurus.Id
                })
                .DistinctBy(n => n.Id)
                .OrderBy(n => n.Id)], default);

            // child rdfs:subClassOf parent, unless existing
            List<int> nodeIds = [.. ids.Values];
            HashSet<(int, int)> existing = [.. (await context.Triples
                .AsNoTracking()
                .Where(t => t.PredicateId == subId &&
                    nodeIds.Contains(t.SubjectId) && t.ObjectId != null)
                .Select(t => new { t.SubjectId, t.ObjectId })
                .ToListAsync())
                .Select(t => (t.SubjectId, t.ObjectId!.Value))];

            ClassChanges changes = new();
            foreach ((string child, string parent) in links)
            {
                Triple triple = new()
                {
                    SubjectId = ids[child],
                    PredicateId = subId,
                    ObjectId = ids[parent]
                };
                if (!existing.Add((triple.SubjectId, triple.ObjectId.Value)))
                    continue;
                context.Triples.Add(CreateTriple(triple));
                changes.Add(triple, aId, subId);
            }
            await context.SaveChangesAsync();
            await UpdateClassesAsync(context, changes, aId, subId, default);
        }, default).GetAwaiter().GetResult();
    }
    #endregion

    #region Node Classes
    /// <summary>
    /// Updates the classes for all the nodes belonging to any class.
    /// This is a potentially long operation, which rebuilds the whole
    /// node classes table.
    /// </summary>
    /// <param name="cancel">The cancel.</param>
    /// <param name="progress">The progress.</param>
    public async Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        await using CadmusGraphDbContext context = GetContext();

        (int aId, int subId) = await RunInTransactionAsync(context,
            () => GetASubIdsAsync(context, cancel), cancel);

        // load all the IDs first, as the connection cannot be used for
        // other commands while reading
        List<int> ids = await context.Nodes.AsNoTracking()
            .Where(n => !n.IsClass)
            .OrderBy(n => n.Id)
            .Select(n => n.Id)
            .ToListAsync(cancel);

        ProgressReport? report = progress != null ? new ProgressReport() : null;
        int oldPercent = 0;

        foreach (int[] chunk in ids.Chunk(CLASS_BATCH_SIZE))
        {
            // as in previous versions, cancelling just stops the update
            if (cancel.IsCancellationRequested) break;
            await PopulateNodeClassesAsync(context, chunk, aId, subId,
                CancellationToken.None);

            if (report != null)
            {
                report.Count += chunk.Length;
                report.Percent = report.Count * 100 / ids.Count;
                if (report.Percent != oldPercent)
                {
                    progress!.Report(report);
                    oldPercent = report.Percent;
                }
            }
        }

        if (report != null)
        {
            report.Percent = 100;
            report.Count = ids.Count;
            progress!.Report(report);
        }
    }
    #endregion
}

/// <summary>
/// Options used to configure <see cref="EfGraphRepository"/>.
/// </summary>
public class EfGraphRepositoryOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }
}
