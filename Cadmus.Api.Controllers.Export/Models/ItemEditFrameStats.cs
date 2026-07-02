using System;

namespace Cadmus.Api.Controllers.Export.Models;

/// <summary>
/// Statistics for an items edit frame.
/// </summary>
public class ItemEditFrameStats
{
    /// <summary>
    /// Start frame time.
    /// </summary>
    public DateTime Start { get; set; }

    /// <summary>
    /// End frame time.
    /// </summary>
    public DateTime End { get; set; }

    /// <summary>
    /// Count of created items in this frame.
    /// </summary>
    public int CreatedCount { get; set; }

    /// <summary>
    /// Count of updated items in this frame.
    /// </summary>
    public int UpdatedCount { get; set; }

    /// <summary>
    /// Count of deleted items in this frame.
    /// </summary>
    public int DeletedCount { get; set; }

    /// <summary>
    /// Returns a string representation of the object.
    /// </summary>
    /// <returns>string</returns>
    public override string ToString()
    {
        return $"{Start}-{End}: C={CreatedCount} U={UpdatedCount} D={DeletedCount}";
    }
}
