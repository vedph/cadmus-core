namespace Cadmus.Refs.Bricks;

/// <summary>
/// A component of a <see cref="ProperName"/>.
/// </summary>
public class ProperNamePiece
{
    /// <summary>
    /// Gets or sets the name piece's type, like first name, last name,
    /// patronymic, epithet, etc.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the name piece's value.
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
        return (string.IsNullOrEmpty(Type) ?
            Value : $"{Value} [{Type}]") ?? base.ToString()!;
    }
}
