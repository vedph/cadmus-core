using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Graph;

/// <summary>
/// A set of graph nodes and triples, derived from one or more mapping
/// sessions.
/// </summary>
public class GraphSet
{
    /// <summary>
    /// Gets the nodes.
    /// </summary>
    public IList<UriNode> Nodes { get; }

    /// <summary>
    /// Gets the triples.
    /// </summary>
    public IList<UriTriple> Triples { get; }

    /// <summary>
    /// Gets the IDs (GUIDs) of the sources (items or parts) whose whole graph
    /// is represented by this set. When updating the graph, any node or
    /// triple previously generated from any of these sources (i.e. having
    /// a SID starting with its ID) and not found in this set is removed,
    /// even when this set contains no node or triple at all for it (e.g.
    /// when the user removed from a part all the entries which were mapped).
    /// Sources not listed here are updated only when the set contains
    /// any node or triple from them. <see cref="GraphUpdater"/> always
    /// adds the ID of the item or part being mapped.
    /// </summary>
    public ISet<string> SourceIds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphSet"/> class.
    /// </summary>
    public GraphSet()
    {
        Nodes = [];
        Triples = [];
        SourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphSet"/> class.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">nodes or triples</exception>
    public GraphSet(IList<UriNode> nodes, IList<UriTriple> triples)
    {
        Nodes = nodes ?? throw new ArgumentNullException(nameof(nodes));
        Triples = triples ?? throw new ArgumentNullException(nameof(triples));
        SourceIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the source GUID from the specified SID. By convention, SIDs
    /// start with the GUID of the item or part they were generated from
    /// (36 characters), optionally followed by other components.
    /// </summary>
    /// <param name="sid">The SID.</param>
    /// <returns>The GUID (or the whole SID when shorter than a GUID), or
    /// an empty string for a null SID.</returns>
    public static string GetSourceGuid(string? sid)
    {
        if (string.IsNullOrEmpty(sid)) return "";
        return sid.Length > 36 ? sid[..36] : sid;
    }

    /// <summary>
    /// Adds the specified nodes to this set, unless they already exist.
    /// </summary>
    /// <param name="nodes">The nodes.</param>
    /// <exception cref="ArgumentNullException">nodes</exception>
    public void AddNodes(IList<UriNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        foreach (UriNode node in nodes)
        {
            if (Nodes.All(n => n.Id != node.Id))
                Nodes.Add(node);
        }
    }

    /// <summary>
    /// Adds the specified triples to this set, unless they already exist.
    /// </summary>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">nodes</exception>
    public void AddTriples(IList<UriTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(triples);

        foreach (UriTriple triple in triples)
        {
            if (Triples.All(t => t.SubjectId != triple.SubjectId ||
                t.PredicateId != triple.PredicateId ||
                t.ObjectId != triple.ObjectId ||
                t.ObjectLiteral != triple.ObjectLiteral))
            {
                Triples.Add(triple);
            }
        }
    }

    /// <summary>
    /// Gets groups of nodes having the same SID's GUID. Usually a set contains
    /// entries from a single GUID source, but some of them may have a null
    /// SID (=the nodes generated as objects of a triple).
    /// </summary>
    /// <returns>Dictionary.</returns>
    public IDictionary<string, IList<UriNode>> GetNodesByGuid()
    {
        Dictionary<string, IList<UriNode>> dct =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (UriNode node in Nodes)
        {
            string key = GetSourceGuid(node.Sid);

            if (!dct.TryGetValue(key, out IList<UriNode>? list))
                dct[key] = list = [];
            list.Add(node);
        }
        return dct;
    }

    /// <summary>
    /// Gets groups of triples having the same SID's GUID. Usually a set
    /// contains entries from a single GUID source, but this enforces
    /// the update constraints.
    /// </summary>
    /// <returns>Dictionary.</returns>
    public IDictionary<string, IList<UriTriple>> GetTriplesByGuid()
    {
        Dictionary<string, IList<UriTriple>> dct =
            new(StringComparer.OrdinalIgnoreCase);

        foreach (UriTriple triple in Triples)
        {
            string key = GetSourceGuid(triple.Sid);

            if (!dct.TryGetValue(key, out IList<UriTriple>? list))
                dct[key] = list = [];
            list.Add(triple);
        }
        return dct;
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"N: {Nodes.Count} | T: {Triples.Count}";
    }
}
