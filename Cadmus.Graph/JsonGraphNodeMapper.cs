using Fusi.Tools.Configuration;

namespace Cadmus.Graph;

/// <summary>
/// JSON-based node mapper building RDF nodes and triples.
/// <para>Tag: <c>node-mapper.json</c>.</para>
/// </summary>
/// <seealso cref="JsonNodeMapper{TTarget}" />
/// <seealso cref="INodeMapper" />
[Tag("node-mapper.json")]
public sealed class JsonGraphNodeMapper : JsonNodeMapper<GraphSet>, INodeMapper
{
    private void AddNodes(string sid, GraphNodeMapping mapping, GraphSet target)
    {
        foreach (var p in mapping.Output!.Nodes)
        {
            string uri = UidBuilder.BuildUid(ResolveTemplate(p.Value.Uid!, true),
                sid);
            UriNode node = new()
            {
                Uri = uri,
                SourceType = SourceType,
                Sid = sid,
                Label = string.IsNullOrEmpty(p.Value.Label) ?
                    uri : ResolveTemplate(p.Value.Label, false)
            };
            ContextNodes[p.Key] = node;
            target.Nodes.Add(node);
        }
    }

    private void AddTriples(string sid, GraphNodeMapping mapping, GraphSet target)
    {
        int n = 0;
        foreach (MappedTriple tripleSource in mapping.Output!.Triples)
        {
            n++;
            if (string.IsNullOrEmpty(tripleSource.S))
            {
                throw new CadmusGraphException(
                    $"Undefined triple subject at mapping #{n}: {mapping}");
            }
            if (string.IsNullOrEmpty(tripleSource.P))
            {
                throw new CadmusGraphException(
                    $"Undefined triple predicate at mapping #{n}: {mapping}");
            }
            if (string.IsNullOrEmpty(tripleSource.O) && tripleSource.OL == null)
            {
                throw new CadmusGraphException(
                    $"Undefined triple object at mapping #{n}: {mapping}");
            }

            UriTriple triple = new()
            {
                Sid = sid,
                SubjectUri = ResolveTemplate(tripleSource.S!, true),
                // P=a becomes rdf:type
                PredicateUri = ResolveTemplate(tripleSource.P == "a"
                    ? "rdf:type" : tripleSource.P!, true),
                ObjectUri = tripleSource.O != null
                    ? ResolveTemplate(tripleSource.O!, true) : null,
                ObjectLiteral = tripleSource.OL != null
                    ? ResolveTemplate(tripleSource.OL, false)
                    : null
            };
            LiteralHelper.AdjustLiteral(triple);
            target.Triples.Add(triple);
        }
    }

    /// <summary>
    /// Builds the RDF nodes and triples for the specified matched mapping.
    /// </summary>
    /// <param name="sid">The SID resolved for this mapping (which might be
    /// inherited from an ancestor mapping), or null/empty when none could
    /// be resolved.</param>
    /// <param name="mapping">The matched mapping.</param>
    /// <param name="target">The target graph set.</param>
    /// <exception cref="CadmusGraphException">Undefined SID for a mapping
    /// requiring one to emit nodes or triples.</exception>
    protected override void BuildOutput(string? sid, NodeMapping mapping,
        GraphSet target)
    {
        GraphNodeMapping gm = (GraphNodeMapping)mapping;
        if (gm.Output == null) return;

        // metadata
        if (gm.Output.HasMetadata)
        {
            foreach (var p in gm.Output.Metadata)
                Data[p.Key] = ResolveTemplate(p.Value!, false);
        }

        if (string.IsNullOrEmpty(sid))
        {
            if (!gm.Output.HasNoGraph)
            {
                throw new CadmusGraphException(
                    $"Undefined SID for mapping {mapping}");
            }
            return;
        }

        // nodes
        if (gm.Output.HasNodes) AddNodes(sid!, gm, target);
        // triples
        if (gm.Output.HasTriples) AddTriples(sid!, gm, target);
    }
}
