namespace Cadmus.General.Parts;

/// <summary>
/// A metadatum used in <see cref="MetadataPart"/>.
/// </summary>
public class Metadatum
{
    /// <summary>
    /// Gets or sets the data type. This is some identifier for the data
    /// type, usually an XSDL data type ID.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"{Name}={Value}" +
            (string.IsNullOrEmpty(Type)? "" : "(" + Type + ")");
    }
}
