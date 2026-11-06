using Cadmus.Core.Config;
using Fusi.Tools;
using Fusi.Tools.Data;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Graph.Test;

internal class MockGraphRepository : RamMappingRepository, IGraphRepository
{
    private readonly RamUidBuilder _uidBuilder;

    public IMemoryCache? Cache { get; set; }

    public MockGraphRepository()
    {
        _uidBuilder = new RamUidBuilder();
    }

    public bool CreateStore(object? payload = null)
    {
        return false;
    }

    public void AddNamespace(string prefix, string uri)
    {
        throw new NotImplementedException();
    }

    public void AddNode(Node node, bool noUpdate = false)
    {
        throw new NotImplementedException();
    }

    public void AddProperty(Property property)
    {
        throw new NotImplementedException();
    }

    public void AddThesaurus(Thesaurus thesaurus, bool includeRoot,
        string? prefix = null)
    {
        throw new NotImplementedException();
    }

    public void AddTriple(Triple triple)
    {
        throw new NotImplementedException();
    }

    public int AddUri(string uri)
    {
        throw new NotImplementedException();
    }

    public string BuildUid(string unsuffixed, string sid)
    {
        return _uidBuilder.BuildUid(unsuffixed, sid);
    }

    public void DeleteGraphSet(string sourceId)
    {
        DeletedSourceIds.Add(sourceId);
    }

    public void DeleteNamespaceByPrefix(string prefix)
    {
        throw new NotImplementedException();
    }

    public void DeleteNamespaceByUri(string uri)
    {
        throw new NotImplementedException();
    }

    public void DeleteNode(int id)
    {
        throw new NotImplementedException();
    }

    public void DeleteProperty(int id)
    {
        throw new NotImplementedException();
    }

    public void DeleteTriple(int id)
    {
        throw new NotImplementedException();
    }

    // same semantics of the database repositories: each filter defined
    // in the mapping must be satisfied by the source
    private static IQueryable<GraphNodeMapping> ApplyNodeMappingFilter(
        RunNodeMappingFilter filter, IQueryable<GraphNodeMapping> mappings)
    {
        // (in our RAM list all the mappings are top-level, so no parent ID
        // filtering is required)
        return mappings.Where(m => m.SourceType == filter.SourceType &&
            (string.IsNullOrEmpty(m.FacetFilter) ||
             m.FacetFilter == filter.Facet) &&
            (string.IsNullOrEmpty(m.GroupFilter) ||
             Regex.IsMatch(filter.Group ?? "", m.GroupFilter)) &&
            (m.FlagsFilter == null ||
             ((filter.Flags ?? 0) & m.FlagsFilter.Value) == m.FlagsFilter.Value) &&
            (string.IsNullOrEmpty(m.TitleFilter) ||
             Regex.IsMatch(filter.Title ?? "", m.TitleFilter)) &&
            (string.IsNullOrEmpty(m.PartTypeFilter) ||
             m.PartTypeFilter == filter.PartType) &&
            (string.IsNullOrEmpty(m.PartRoleFilter) ||
             m.PartRoleFilter == filter.PartRole));
    }

    public IList<GraphNodeMapping> FindMappings(RunNodeMappingFilter filter)
    {
        return new List<GraphNodeMapping>(
            ApplyNodeMappingFilter(filter, Mappings.AsQueryable()));
    }

    public GraphSet GetGraphSet(string sourceId)
    {
        GraphSet set = new();
        set.SourceIds.Add(sourceId);
        return set;
    }

    public DataPage<UriTriple> GetLinkedLiterals(LinkedLiteralFilter filter)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriNode> GetLinkedNodes(LinkedNodeFilter filter)
    {
        throw new NotImplementedException();
    }

    public DataPage<NamespaceEntry> GetNamespaces(NamespaceFilter filter)
    {
        throw new NotImplementedException();
    }

    public UriNode? GetNode(int id)
    {
        throw new NotImplementedException();
    }

    public UriNode? GetNodeByUri(string uri)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriNode> GetNodes(NodeFilter filter)
    {
        throw new NotImplementedException();
    }

    public IList<UriNode?> GetNodes(IList<int> ids)
    {
        throw new NotImplementedException();
    }

    public DataPage<UriProperty> GetProperties(PropertyFilter filter)
    {
        throw new NotImplementedException();
    }

    public UriProperty? GetProperty(int id)
    {
        throw new NotImplementedException();
    }

    public UriProperty? GetPropertyByUri(string uri)
    {
        throw new NotImplementedException();
    }

    public UriTriple? GetTriple(int id)
    {
        throw new NotImplementedException();
    }

    public DataPage<TripleGroup> GetTripleGroups(TripleFilter filter,
        string sort = "Cu")
    {
        throw new NotImplementedException();
    }

    public DataPage<UriTriple> GetTriples(TripleFilter filter)
    {
        throw new NotImplementedException();
    }

    public void ImportNodes(IEnumerable<UriNode> nodes)
    {
        throw new NotImplementedException();
    }

    public void ImportTriples(IEnumerable<UriTriple> triples)
    {
        throw new NotImplementedException();
    }

    public int LookupId(string uri)
    {
        throw new NotImplementedException();
    }

    public string? LookupNamespace(string prefix)
    {
        throw new NotImplementedException();
    }

    public string? LookupUri(int id)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    /// The sets received by <see cref="UpdateGraph"/>.
    /// </summary>
    public List<GraphSet> UpdatedSets { get; } = [];

    /// <summary>
    /// The source IDs received by <see cref="DeleteGraphSet"/>.
    /// </summary>
    public List<string> DeletedSourceIds { get; } = [];

    public void UpdateGraph(GraphSet set)
    {
        UpdatedSets.Add(set);
    }

    public Task UpdateNodeClassesAsync(CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        throw new NotImplementedException();
    }
}
