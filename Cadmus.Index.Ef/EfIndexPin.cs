using Cadmus.Core;
using System;

namespace Cadmus.Index.Ef;

/// <summary>
/// Index entity for a data pin.
/// </summary>
public class EfIndexPin
{
    /// <summary>
    /// Gets or sets the pin ID.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public string ItemId { get; set; }

    /// <summary>
    /// Gets or sets the part ID.
    /// </summary>
    public string PartId { get; set; }

    /// <summary>
    /// Gets or sets the part type ID.
    /// </summary>
    public string PartTypeId { get; set; }

    /// <summary>
    /// Gets or sets the part role ID.
    /// </summary>
    public string? PartRoleId { get; set; }

    /// <summary>
    /// Gets or sets the pin name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the pin value.
    /// </summary>
    public string Value { get; set; }

    /// <summary>
    /// Gets or sets the time the pin was indexed.
    /// </summary>
    public DateTime TimeIndexed { get; set; }

    /// <summary>
    /// Gets or sets the associated index item.
    /// </summary>
    public EfIndexItem? Item { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EfIndexPin"/> class.
    /// </summary>
    public EfIndexPin()
    {
        ItemId = "";
        PartId = "";
        PartTypeId = "";
        Name = "";
        Value = "";
    }

    /// <summary>
    /// Converts this instance into a <see cref="DataPinInfo"/> object.
    /// </summary>
    /// <returns>A <see cref="DataPinInfo"/> object representing this pin.</returns>
    public DataPinInfo ToDataPinInfo()
    {
        return new DataPinInfo
        {
            ItemId = ItemId,
            PartId = PartId,
            RoleId = PartRoleId,
            Name = Name,
            Value = Value,
            PartTypeId = PartTypeId,
        };
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"[{PartTypeId}.{PartRoleId ?? "-"}] {Name}={Value}";
    }
}
