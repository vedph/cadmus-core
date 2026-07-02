namespace Cadmus.Refs.Bricks;

/// <summary>
/// A count decorated with an ID and an optional tag and/or note.
/// </summary>
public class DecoratedCount
{
    /// <summary>
    /// Gets or sets the identifier of the entity being counted.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Gets or sets a generic tag optionally used to group or classify
    /// counts.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the count value.
    /// </summary>
    public int Value { get; set; }

    /// <summary>
    /// Gets or sets an optional note.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"{Id}={Value}" + (string.IsNullOrEmpty(Tag) ? "" : $" ({Tag})");
    }
}
