using Cadmus.Core.Config;
using Cadmus.Graph;
using Cadmus.Graph.Ef;
using Cadmus.Graph.Ef.PgSql;
using CadmusApi.Controllers;
using Fusi.DbManager.PgSql;
using Fusi.Tools;
using Fusi.Tools.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CadmusApi.Services;

/// <summary>
/// A graph repository for the walker demo page. This is a local service
/// used to provide the graph repository for the walker demo page. The required
/// database, named <c>cadmus-graph-demo</c>, is lazily created and seeded
/// with constant data if necessary.
/// </summary>
public sealed class WalkerDemoGraphRepository : IGraphRepository
{
    private const string DB_NAME = "cadmus-graph-demo";

    private readonly EfPgSqlGraphRepository _inner = new();
    private readonly IConfiguration _config;
    private bool _seeded = false;

    public WalkerDemoGraphRepository(IConfiguration config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    #region Seeding
    static private Stream GetResourceStream(string name) =>
        typeof(WalkerDemoGraphController).Assembly.GetManifestResourceStream(
        "CadmusApi.Assets." + name)!;

    private static void FillGraph(IGraphRepository repository)
    {
        JsonGraphPresetReader reader = new();

        // nodes
        using (Stream stream = GetResourceStream("Petrarch-n.json"))
        using (ItemFlusher<UriNode> nodeFlusher = new(nodes =>
            repository.ImportNodes(nodes)))
        {
            foreach (UriNode node in reader.ReadNodes(stream))
                nodeFlusher.Add(node);
        }

        // triples
        using (Stream stream = GetResourceStream("Petrarch-t.json"))
        using (ItemFlusher<UriTriple> tripleFlusher = new(triples =>
            repository.ImportTriples(triples)))
        {
            foreach (UriTriple triple in reader.ReadTriples(stream))
                tripleFlusher.Add(triple);
        }
    }

    private static void SeedGraphDatabase(
        IGraphRepository repository,
        IConfiguration config)
    {
        // nope if database exists
        string cst = config.GetConnectionString("Index")!;

        PgSqlDbManager dbManager = new(cst);
        if (dbManager.Exists(DB_NAME)) return;

        // else create and seed it
        dbManager.CreateDatabase(DB_NAME, EfPgSqlGraphRepository.GetSchema(), null);

        // fill with sample data
        FillGraph(repository);
    }
    #endregion

    public IMemoryCache? Cache
    {
        get => _inner.Cache;
        set => _inner.Cache = value;
    }

    private void EnsureSeeded()
    {
        if (!_seeded)
        {
            // nope if database exists
            string cs = string.Format(_config.GetConnectionString("Index")!,
                DB_NAME);

            _inner.Configure(new EfGraphRepositoryOptions
            {
                ConnectionString = cs
            });

            SeedGraphDatabase(_inner, _config);
            _seeded = true;
        }
    }

    public int AddMapping(NodeMapping mapping)
    {
        EnsureSeeded();
        return _inner.AddMapping(mapping);
    }

    public void AddNamespace(string prefix, string uri)
    {
        EnsureSeeded();
        _inner.AddNamespace(prefix, uri);
    }

    public void AddNode(Node node, bool noUpdate = false)
    {
        EnsureSeeded();
        _inner.AddNode(node, noUpdate);
    }

    public void AddProperty(Property property)
    {
        EnsureSeeded();
        _inner.AddProperty(property);
    }

    public void AddThesaurus(Thesaurus thesaurus, bool includeRoot,
        string? prefix = null)
    {
        EnsureSeeded();
        _inner.AddThesaurus(thesaurus, includeRoot, prefix);
    }

    public void AddTriple(Triple triple)
    {
        EnsureSeeded();
        _inner.AddTriple(triple);
    }

    public int AddUri(string uri)
    {
        EnsureSeeded();
        return _inner.AddUri(uri);
    }

    public string BuildUid(string unsuffixed, string sid)
    {
        EnsureSeeded();
        return _inner.BuildUid(unsuffixed, sid);
    }

    public bool CreateStore(object? payload = null)
    {
        EnsureSeeded();
        return _inner.CreateStore(payload);
    }

    public void DeleteGraphSet(string sourceId)
    {
        EnsureSeeded();
        _inner.DeleteGraphSet(sourceId);
    }

    public void DeleteMapping(int id)
    {
        EnsureSeeded();
        _inner.DeleteMapping(id);
    }

    public void DeleteNamespaceByPrefix(string prefix)
    {
        EnsureSeeded();
        _inner.DeleteNamespaceByPrefix(prefix);
    }

    public void DeleteNamespaceByUri(string uri)
    {
        EnsureSeeded();
        _inner.DeleteNamespaceByUri(uri);
    }

    public void DeleteNode(int id)
    {
        EnsureSeeded();
        _inner.DeleteNode(id);
    }

    public void DeleteProperty(int id)
    {
        EnsureSeeded();
        _inner.DeleteProperty(id);
    }

    public void DeleteTriple(int id)
    {
        EnsureSeeded();
        _inner.DeleteTriple(id);
    }

    public string Export()
    {
        EnsureSeeded();
        return _inner.Export();
    }

    public IList<NodeMapping> FindMappings(RunNodeMappingFilter filter)
    {
        EnsureSeeded();
        return _inner.FindMappings(filter);
    }

    public GraphSet GetGraphSet(string sourceId)
    {
        EnsureSeeded();
        return _inner.GetGraphSet(sourceId);
    }

    public DataPage<UriTriple> GetLinkedLiterals(LinkedLiteralFilter filter)
    {
        EnsureSeeded();
        return _inner.GetLinkedLiterals(filter);
    }

    public DataPage<UriNode> GetLinkedNodes(LinkedNodeFilter filter)
    {
        EnsureSeeded();
        return _inner.GetLinkedNodes(filter);
    }

    public NodeMapping? GetMapping(int id)
    {
        EnsureSeeded();
        return _inner.GetMapping(id);
    }

    public DataPage<NodeMapping> GetMappings(NodeMappingFilter filter,
        bool descendants)
    {
        EnsureSeeded();
        return _inner.GetMappings(filter, descendants);
    }

    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter)
    {
        EnsureSeeded();
        return _inner.GetNamespaces(filter);
    }

    public UriNode? GetNode(int id)
    {
        EnsureSeeded();
        return _inner.GetNode(id);
    }

    public UriNode? GetNodeByUri(string uri)
    {
        EnsureSeeded();
        return _inner.GetNodeByUri(uri);
    }

    public DataPage<UriNode> GetNodes(NodeFilter filter)
    {
        EnsureSeeded();
        return _inner.GetNodes(filter);
    }

    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        EnsureSeeded();
        return _inner.GetNodes(ids);
    }

    public DataPage<UriProperty> GetProperties(PropertyFilter filter)
    {
        EnsureSeeded();
        return _inner.GetProperties(filter);
    }

    public UriProperty? GetProperty(int id)
    {
        EnsureSeeded();
        return GetProperty(id);
    }

    public UriProperty? GetPropertyByUri(string uri)
    {
        EnsureSeeded();
        return _inner.GetPropertyByUri(uri);
    }

    public UriTriple? GetTriple(int id)
    {
        EnsureSeeded();
        return GetTriple(id);
    }

    public DataPage<TripleGroup> GetTripleGroups(TripleFilter filter,
        string sort = "Cu")
    {
        EnsureSeeded();
        return _inner.GetTripleGroups(filter, sort);
    }

    public DataPage<UriTriple> GetTriples(TripleFilter filter)
    {
        EnsureSeeded();
        return _inner.GetTriples(filter);
    }

    public int Import(string json)
    {
        EnsureSeeded();
        return _inner.Import(json);
    }

    public void ImportNodes(IEnumerable<UriNode> nodes)
    {
        EnsureSeeded();
        _inner.ImportNodes(nodes);
    }

    public void ImportTriples(IEnumerable<UriTriple> triples)
    {
        EnsureSeeded();
        _inner.ImportTriples(triples);
    }

    public int LookupId(string uri)
    {
        EnsureSeeded();
        return _inner.LookupId(uri);
    }

    public string? LookupNamespace(string prefix)
    {
        EnsureSeeded();
        return _inner.LookupNamespace(prefix);
    }

    public string? LookupUri(int id)
    {
        EnsureSeeded();
        return _inner.LookupUri(id);
    }

    public void UpdateGraph(GraphSet set)
    {
        EnsureSeeded();
        _inner.UpdateGraph(set);
    }

    public Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        EnsureSeeded();
        return _inner.UpdateNodeClassesAsync(cancel, progress);
    }
}
