using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Generic flags part. This contains a thesaurus-defined set of binary flags,
/// possibly having a note for each flag.
/// <para>Tag: <c>it.vedph.flags</c>.</para>
/// </summary>
[Tag("it.vedph.flags")]
public sealed class FlagsPart : PartBase
{
    /// <summary>
    /// The flags. Usually from thesaurus <c>flags</c>.
    /// </summary>
    public HashSet<string> Flags { get; set; } = [];

    /// <summary>
    /// An optional dictionary of notes, keyed by flag.
    /// </summary>
    public Dictionary<string, string>? Notes { get; set; }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();
        builder.AddValues("flag", Flags);
        return builder.Build(this);
    }

    /// <summary>
    /// Gets the definitions of data pins used by the implementor.
    /// </summary>
    /// <returns>Data pins definitions.</returns>
    public override IList<DataPinDefinition> GetDataPinDefinitions()
    {
        return new List<DataPinDefinition>(
        [
             new DataPinDefinition(DataPinValueType.String,
                "flag",
                "The flag(s) set.",
                "M")
        ]);
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

        sb.Append("[Flags] ");
        sb.AppendJoin(", ", Flags);

        return sb.ToString();
    }
}
