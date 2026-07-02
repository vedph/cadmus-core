namespace Cadmus.Export.Rdf;

/// <summary>
/// A property with additional metadata.
/// </summary>
public class RdfProperty
{
    /// <summary>
    /// The property's numeric ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The property's data type.
    /// </summary>
    public string? DataType { get; set; }

    /// <summary>
    /// The property's literal editor, if any.
    /// </summary>
    public string? LitEditor { get; set; }

    /// <summary>
    /// The property's description.
    /// </summary>
    public string? Description { get; set; }
}
