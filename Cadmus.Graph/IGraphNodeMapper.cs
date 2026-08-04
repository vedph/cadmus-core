using Cadmus.Export.Mapping;

namespace Cadmus.Graph;

/// <summary>
/// Graph node mapper interface. This mapper targets an RDF graph.
/// </summary>
public interface IGraphNodeMapper : INodeMapper<GraphSet>
{
    /// <summary>
    /// Gets or sets the URI builder function. This is used to build URIs
    /// from SID and UID.
    /// </summary>
    IUidBuilder UidBuilder { get; set; }
}
