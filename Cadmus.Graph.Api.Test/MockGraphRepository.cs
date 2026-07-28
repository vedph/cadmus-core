using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cadmus.Core.Config;
using Fusi.Tools;
using Fusi.Tools.Data;
using Microsoft.Extensions.Caching.Memory;

namespace Cadmus.Graph.Api.Test;

/// <summary>
/// Minimal <see cref="IGraphRepository"/> test double for controller tests:
/// only the members invoked by <see cref="Controllers.GraphController"/> are
/// meaningfully implemented (configurable via the public fields/funcs
/// below); every other member throws <see cref="NotImplementedException"/>.
/// </summary>
internal sealed class MockGraphRepository : IGraphRepository
{
    public IMemoryCache? Cache { get; set; }

    public NodeFilter? LastNodeFilter { get; private set; }
    public DataPage<UriNode> NodesResult { get; set; } =
        new(1, 10, 0, []);

    public int LastNodeId { get; private set; } = -1;
    public UriNode? NodeResult { get; set; }

    public IList<int>? LastNodeIds { get; private set; }
    public IList<UriNode?> NodeSetResult { get; set; } = [];

    public string? LastUri { get; private set; }
    public UriNode? NodeByUriResult { get; set; }

    public TripleFilter? LastTripleFilter { get; private set; }
    public string? LastSort { get; private set; }
    public DataPage<TripleGroup> TripleGroupsResult { get; set; } =
        new(1, 10, 0, []);

    public LinkedNodeFilter? LastLinkedNodeFilter { get; private set; }
    public DataPage<UriNode> LinkedNodesResult { get; set; } =
        new(1, 10, 0, []);

    public LinkedLiteralFilter? LastLinkedLiteralFilter { get; private set; }
    public DataPage<UriTriple> LinkedLiteralsResult { get; set; } =
        new(1, 10, 0, []);

    public DataPage<UriNode> GetNodes(NodeFilter filter)
    {
        LastNodeFilter = filter;
        return NodesResult;
    }

    public UriNode? GetNode(int id)
    {
        LastNodeId = id;
        return NodeResult;
    }

    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        LastNodeIds = ids;
        return NodeSetResult;
    }

    public UriNode? GetNodeByUri(string uri)
    {
        LastUri = uri;
        return NodeByUriResult;
    }

    public DataPage<TripleGroup> GetTripleGroups(TripleFilter filter,
        string sort = "Cu")
    {
        LastTripleFilter = filter;
        LastSort = sort;
        return TripleGroupsResult;
    }

    public DataPage<UriNode> GetLinkedNodes(LinkedNodeFilter filter)
    {
        LastLinkedNodeFilter = filter;
        return LinkedNodesResult;
    }

    public DataPage<UriTriple> GetLinkedLiterals(LinkedLiteralFilter filter)
    {
        LastLinkedLiteralFilter = filter;
        return LinkedLiteralsResult;
    }

    #region Not implemented
    public bool CreateStore(object? payload = null) =>
        throw new NotImplementedException();
    public void AddNamespace(string prefix, string uri) =>
        throw new NotImplementedException();
    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter) =>
        throw new NotImplementedException();
    public string? LookupNamespace(string prefix) =>
        throw new NotImplementedException();
    public void DeleteNamespaceByPrefix(string prefix) =>
        throw new NotImplementedException();
    public void DeleteNamespaceByUri(string uri) =>
        throw new NotImplementedException();

    public string BuildUid(string unsuffixed, string sid) =>
        throw new NotImplementedException();
    public int AddUri(string uri) => throw new NotImplementedException();
    public string? LookupUri(int id) => throw new NotImplementedException();
    public int LookupId(string uri) => throw new NotImplementedException();

    public void AddNode(Node node, bool noUpdate = false) =>
        throw new NotImplementedException();
    public void ImportNodes(IEnumerable<UriNode> nodes) =>
        throw new NotImplementedException();
    public void DeleteNode(int id) => throw new NotImplementedException();

    public DataPage<UriProperty> GetProperties(PropertyFilter filter) =>
        throw new NotImplementedException();
    public UriProperty? GetProperty(int id) =>
        throw new NotImplementedException();
    public UriProperty? GetPropertyByUri(string uri) =>
        throw new NotImplementedException();
    public void AddProperty(Property property) =>
        throw new NotImplementedException();
    public void DeleteProperty(int id) =>
        throw new NotImplementedException();

    public DataPage<NodeMapping> GetMappings(NodeMappingFilter filter,
        bool descendants) => throw new NotImplementedException();
    public NodeMapping? GetMapping(int id) =>
        throw new NotImplementedException();
    public int AddMapping(NodeMapping mapping) =>
        throw new NotImplementedException();
    public void DeleteMapping(int id) => throw new NotImplementedException();
    public IList<NodeMapping> FindMappings(RunNodeMappingFilter filter) =>
        throw new NotImplementedException();
    public int Import(string json) => throw new NotImplementedException();
    public string Export() => throw new NotImplementedException();

    public DataPage<UriTriple> GetTriples(TripleFilter filter) =>
        throw new NotImplementedException();
    public UriTriple? GetTriple(int id) =>
        throw new NotImplementedException();
    public void AddTriple(Triple triple) =>
        throw new NotImplementedException();
    public void ImportTriples(IEnumerable<UriTriple> triples) =>
        throw new NotImplementedException();
    public void DeleteTriple(int id) => throw new NotImplementedException();

    public void AddThesaurus(Thesaurus thesaurus, bool includeRoot,
        string? prefix = null) => throw new NotImplementedException();

    public Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null) =>
        throw new NotImplementedException();

    public GraphSet GetGraphSet(string sourceId) =>
        throw new NotImplementedException();
    public void DeleteGraphSet(string sourceId) =>
        throw new NotImplementedException();
    public void UpdateGraph(GraphSet set) =>
        throw new NotImplementedException();
    #endregion
}
