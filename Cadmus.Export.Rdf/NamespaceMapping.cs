namespace Cadmus.Export.Rdf;

/// <summary>
/// A namespace prefix mapping.
/// </summary>
public class NamespaceMapping
{
    /// <summary>
    /// The prefix.
    /// </summary>
    public required string Prefix { get; set; }

    /// <summary>
    /// The corresponding URI.
    /// </summary>
    public required string Uri { get; set; }

    /// <summary>
    /// String representation of this mapping.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{Prefix} = {Uri}";
    }
}
