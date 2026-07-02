using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Cadmus.Mat.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// A set of physical preservation states.
/// <para>Tag: <c>it.vedph.physical-states</c>.</para>
/// </summary>
[Tag("it.vedph.physical-states")]
public sealed class PhysicalStatesPart : PartBase
{
    /// <summary>
    /// Gets or sets the entries.
    /// </summary>
    public List<PhysicalState> States { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalStatesPart"/> class.
    /// </summary>
    public PhysicalStatesPart()
    {
        States = [];
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>type-X-count</c> where <c>X</c> is the type ID and the
    /// value is the count of states of that type.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();

        builder.Set("tot", States?.Count ?? 0, false);

        if (States?.Count > 0)
        {
            foreach (PhysicalState state in States)
            {
                builder.Increase(state.Type, false, "type-");
            }
        }

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
            new DataPinDefinition(DataPinValueType.Integer,
               "tot-count",
               "The total count of entries."),
            new DataPinDefinition(DataPinValueType.Integer,
               "type-<X>",
               "The count of entries of the specified type, where X is the type ID.")
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

        sb.Append("[PhysicalStates]");

        if (States?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in States)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (States.Count > 3)
                sb.Append("...(").Append(States.Count).Append(')');
        }

        return sb.ToString();
    }
}
