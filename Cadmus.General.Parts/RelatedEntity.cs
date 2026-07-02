using Cadmus.Refs.Bricks;

namespace Cadmus.General.Parts;

/// <summary>
/// Any entity related to an <see cref="HistoricalEvent"/>.
/// </summary>
public class RelatedEntity
{
    /// <summary>
    /// Gets or sets the type of relation connecting this entity to the event.
    /// </summary>
    public string? Relation { get; set; }

    /// <summary>
    /// Gets or sets the entity ID.
    /// </summary>
    public AssertedCompositeId? Id { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return $"{Relation}: {Id}";
    }
}
