using System.Collections.Generic;
using System.Text;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// An assertion connected to some statement to qualify its level of
/// certainty and eventually allege documental references supporting it.
/// </summary>
public class Assertion
{
    /// <summary>
    /// Gets or sets the optional tag. This is an arbitrary value used
    /// to classify or group assertions.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the assertion's rank; 0=undefined, 1-N a rank where
    /// the higher the number, the less the assertion is considered probable.
    /// </summary>
    public short Rank { get; set; }

    /// <summary>
    /// Gets or sets the optional documental references supporting this
    /// assertion.
    /// </summary>
    public List<DocReference>? References { get; set; }

    /// <summary>
    /// Gets or sets an optional short note.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Assertion"/> class.
    /// </summary>
    public Assertion()
    {
        References = new List<DocReference>();
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        if (!string.IsNullOrEmpty(Tag))
            sb.Append('[').Append(Tag).Append("] ");
        sb.Append(Rank);
        if (References?.Count > 0)
            sb.Append(" (").Append(References.Count).Append(')');

        return sb.ToString();
    }
}
