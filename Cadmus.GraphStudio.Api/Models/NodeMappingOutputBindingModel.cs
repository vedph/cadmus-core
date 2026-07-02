using Cadmus.Graph;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.GraphStudio.Api.Models;

public class NodeMappingOutputBindingModel
{
    /// <summary>
    /// The nodes to emit, keyed under some mapping-scoped ID. This ID can
    /// then be used to recall the node's UID or its other properties.
    /// </summary>
    public Dictionary<string, MappedNode>? Nodes { get; set; }

    /// <summary>
    /// The triples to emit.
    /// </summary>
    public List<MappedTriple>? Triples { get; set; }

    /// <summary>
    /// The metadata pushed into the mapping context.
    /// All these metadata are of type string, as they come from the
    /// mapping definition.
    /// </summary>
    public Dictionary<string, string>? Metadata { get; set; }

    public NodeMappingOutput ToNodeMappingOutput()
    {
        return new NodeMappingOutput
        {
            Nodes = Nodes?.ToDictionary(
                n => n.Key,
                n => n.Value) ?? new Dictionary<string, MappedNode>(),
            Triples = Triples?.Select(t => t).ToArray()
                ?? Array.Empty<MappedTriple>(),
            Metadata = Metadata?.ToDictionary(
                m => m.Key,
                m => m.Value) ?? new Dictionary<string, string>()
        };
    }
}
