namespace Cadmus.Refs.Bricks;

/// <summary>
/// An <see cref="ExternalId"/> with a rank.
/// </summary>
/// <seealso cref="ExternalId" />
public class RankedExternalId : ExternalId
{
    /// <summary>
    /// Gets or sets the rank.
    /// </summary>
    public short Rank { get; set; }
}
