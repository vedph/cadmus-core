using Cadmus.Core;
using System;
using System.Collections.Generic;

namespace Cadmus.Index.Ef;

/// <summary>
/// Index entity for an item.
/// </summary>
public class EfIndexItem
{
    /// <summary>
    /// Gets or sets the item ID.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the item title.
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Gets or sets the item description.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the item facet ID.
    /// </summary>
    public string FacetId { get; set; }

    /// <summary>
    /// Gets or sets the item group ID.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the item sort key.
    /// </summary>
    public string SortKey { get; set; }

    /// <summary>
    /// Gets or sets the item flags.
    /// </summary>
    public int Flags { get; set; }

    /// <summary>
    /// Gets or sets the item creation time.
    /// </summary>
    public DateTime TimeCreated { get; set; }

    /// <summary>
    /// Gets or sets the item creator ID.
    /// </summary>
    public string? CreatorId { get; set; }

    /// <summary>
    /// Gets or sets the item modification time.
    /// </summary>
    public DateTime TimeModified { get; set; }

    /// <summary>
    /// Gets or sets the item user ID.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the item data pins.
    /// </summary>
    public List<EfIndexPin>? Pins { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="EfIndexItem"/> class.
    /// </summary>
    public EfIndexItem()
    {
        Id = "";
        Title = "";
        Description = "";
        FacetId = "";
        SortKey = "";
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EfIndexItem"/> class
    /// from the specified item.
    /// </summary>
    /// <param name="item">The item to copy values from.</param>
    /// <exception cref="ArgumentNullException">Thrown when the item is null.
    /// </exception>
    public EfIndexItem(IItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        Id = item.Id;
        Title = item.Title;
        Description = item.Description;
        FacetId = item.FacetId;
        GroupId = item.GroupId;
        SortKey = item.SortKey;
        Flags = item.Flags;
        TimeCreated = item.TimeCreated;
        CreatorId = item.CreatorId;
        TimeModified = item.TimeModified;
        UserId = item.UserId;
    }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"{Id}: {Title} ({FacetId})";
    }
}
