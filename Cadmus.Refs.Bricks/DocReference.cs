using System.Text;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A general purpose documental reference.
/// </summary>
public class DocReference
{
    /// <summary>
    /// Gets or sets the reference type (e.g. ancient work, bibliographic
    /// entry, manuscript, etc.).
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the optional tag. This is an arbitrary value used
    /// to classify or group references.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the citation. The conventions adopted in this value
    /// depend on the <see cref="Type"/>. For instance, you might have
    /// a cited work with author + comma + title + comma + location,
    /// or just an author name followed by a year, etc.
    /// </summary>
    public string? Citation { get; set; }

    /// <summary>
    /// Gets or sets an optional short note.
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
        StringBuilder sb = new();

        if (!string.IsNullOrEmpty(Type))
            sb.Append('<').Append(Type).Append("> ");

        if (!string.IsNullOrEmpty(Tag))
            sb.Append('[').Append(Tag).Append("] ");

        sb.Append(Citation);
        return sb.ToString();
    }
}
