using System;
using System.Text;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A generic ID from an external resource.
/// </summary>
public class ExternalId
{
    /// <summary>
    /// Gets or sets an optional tag.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the ID value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Gets or sets the optional human-friendly label.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Gets or sets the scope of this ID.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalId"/> class.
    /// </summary>
    public ExternalId()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExternalId"/> class.
    /// </summary>
    /// <param name="id">The identifier to set this ID to.</param>
    /// <exception cref="ArgumentNullException">id</exception>
    public ExternalId(ExternalId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        Tag = id.Tag;
        Value = id.Value;
        Label = id.Label;
        Scope = id.Scope;
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
        if (!string.IsNullOrEmpty(Value)) sb.Append('#').Append(Value);
        if (!string.IsNullOrEmpty(Scope)) sb.Append(" [").Append(Tag).Append(']');
        if (!string.IsNullOrEmpty(Label)) sb.Append(": ").Append(Label);
        return sb.ToString();
    }
}
