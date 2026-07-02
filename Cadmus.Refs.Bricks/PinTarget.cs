using System.Text;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A pin-based link target entry used in <see cref="AssertedCompositeId"/>.
/// </summary>
public class PinTarget
{
    /// <summary>
    /// Gets or sets the GID (global ID).
    /// </summary>
    public string Gid { get; set; }

    /// <summary>
    /// Gets or sets the link's human-friendly label.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets the item identifier.
    /// </summary>
    public string? ItemId { get; set; }

    /// <summary>
    /// Gets or sets the part identifier.
    /// </summary>
    public string? PartId { get; set; }

    /// <summary>
    /// Gets or sets the part type identifier.
    /// </summary>
    public string? PartTypeId { get; set; }

    /// <summary>
    /// Gets or sets the role identifier.
    /// </summary>
    public string? RoleId { get; set; }

    /// <summary>
    /// Gets or sets the pin's name.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the pin's value.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinTarget"/> class.
    /// </summary>
    public PinTarget()
    {
        Gid = "";
        Label = "";
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
        sb.Append(Label).Append(": ").Append(ItemId).Append(' ').Append(PartId);
        if (!string.IsNullOrEmpty(RoleId))
            sb.Append("R=").Append(RoleId);
        return sb.ToString();
    }
}
