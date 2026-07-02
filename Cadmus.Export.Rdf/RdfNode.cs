namespace Cadmus.Export.Rdf;

/// <summary>
/// A node in the RDF graph.
/// </summary>
public class RdfNode
{
    /// <summary>
    /// The numeric ID of the node in the source database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// True if the node is a class, false if it is a property.
    /// </summary>
    public bool IsClass { get; set; }

    /// <summary>
    /// An optional tag for the node, used for filtering.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// The node's human-readable label.
    /// </summary>
    public required string Label { get; set; }

    /// <summary>
    /// The source type of the node (1=item, 2=part, 3=version).
    /// </summary>
    public required int SourceType { get; set; }

    /// <summary>
    /// The source ID of the node in the mapping process which generated it.
    /// </summary>
    public string? Sid { get; set; }

    /// <summary>
    /// Returns a string representation of the object.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{Id}: {Label} ({SourceType})";
    }
}
