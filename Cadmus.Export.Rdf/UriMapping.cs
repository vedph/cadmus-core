namespace Cadmus.Export.Rdf;

/// <summary>
/// Represents a URI mapping between numeric ID and string URI.
/// </summary>
public class UriMapping
{
    /// <summary>
    /// The numeric ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The string URI.
    /// </summary>
    public required string Uri { get; set; }

    /// <summary>
    /// String representation of the mapping.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{Id} = {Uri}";
    }
}
