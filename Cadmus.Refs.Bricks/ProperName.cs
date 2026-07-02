using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A generic proper name, composed of <see cref="ProperNamePiece"/>'s.
/// </summary>
public class ProperName
{
    /// <summary>
    /// Gets or sets the language. Usually this is an ISO639-3 identifier.
    /// </summary>
    public string? Language { get; set; }

    /// <summary>
    /// Gets or sets the optional tag, used to group several person names;
    /// this can be useful when a person has several names.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the parts of this name, in their conventional order.
    /// Note that parts types are not unique in a name: for instance, you
    /// might have a person with 2 first names.
    /// </summary>
    public List<ProperNamePiece>? Pieces { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ProperName"/> class.
    /// </summary>
    public ProperName()
    {
        Pieces = new List<ProperNamePiece>();
    }

    /// <summary>
    /// Gets the full name, built by concatenating all of its parts values.
    /// </summary>
    /// <returns>The full name, eventually empty if no parts.</returns>
    public string GetFullName() =>
        Pieces?.Count > 0 ? string.Join(" ", from p in Pieces select p.Value) : "";

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        if (Pieces == null || Pieces.Count == 0) return base.ToString()!;

        StringBuilder sb = new(string.Join(" ", Pieces));
        if (!string.IsNullOrEmpty(Tag))
            sb.Append(" [").Append(Tag).Append(']');
        if (!string.IsNullOrEmpty(Language))
            sb.Append(" <").Append(Language).Append('>');
        return sb.ToString();
    }
}
