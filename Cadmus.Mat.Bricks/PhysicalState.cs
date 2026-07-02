using System;
using System.Collections.Generic;
using System.Text;

namespace Cadmus.Mat.Bricks;

/// <summary>
/// Physical preservation state of some object.
/// </summary>
public class PhysicalState
{
    /// <summary>
    /// Gets or sets the state type (e.g. bad, good, excellent...).
    /// </summary>
    public string Type { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional features attached to the description of this state.
    /// </summary>
    public IList<string>? Features { get; set; }

    /// <summary>
    /// Gets or sets the date of the last check.
    /// </summary>
    public DateOnly? Date { get; set; }

    /// <summary>
    /// Gets or sets the name or other identifier of the reporter who last
    /// checked the object's state.
    /// </summary>
    public string? Reporter { get; set; }

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
        StringBuilder sb = new(Type);

        if (Features?.Count > 0)
            sb.Append(": ").AppendJoin(", ", Features);

        if (Date != null)
            sb.Append(' ').Append(Date);

        if (!string.IsNullOrEmpty(Reporter))
            sb.Append(" (").Append(Reporter).Append(')');

        return sb.ToString();
    }
}
