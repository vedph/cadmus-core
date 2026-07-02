using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// A set of place/time indications with optional assertions and documental
/// references.
/// <para>Tag: <c>it.vedph.chronotopes</c>.</para>
/// </summary>
[Tag("it.vedph.chronotopes")]
public sealed class ChronotopesPart : PartBase
{
    /// <summary>
    /// Gets or sets the entries.
    /// </summary>
    public List<AssertedChronotope> Chronotopes { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ChronotopesPart"/> class.
    /// </summary>
    public ChronotopesPart()
    {
        Chronotopes = new List<AssertedChronotope>();
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>place</c>, <c>date-value</c>.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new(
            DataPinHelper.DefaultFilter);

        builder.Set("tot", Chronotopes?.Count ?? 0, false);

        if (Chronotopes?.Count > 0)
        {
            foreach (var entry in Chronotopes)
            {
                builder.AddValue("place", entry.Place?.Value, filter: true,
                    filterOptions: true);
                if (entry.Date is not null)
                    builder.AddValue("date-value", entry.Date.GetSortValue());
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
        return new List<DataPinDefinition>(new[]
        {
            new DataPinDefinition(DataPinValueType.Integer,
               "tot-count",
               "The total count of entries."),
            new DataPinDefinition(DataPinValueType.String,
               "place",
               "The list of places.",
               "Mf"),
            new DataPinDefinition(DataPinValueType.Decimal,
               "date-value",
               "The list of date values.",
               "M")
        });
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

        sb.Append("[Chronotopes]");

        if (Chronotopes?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Chronotopes)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Chronotopes.Count > 3)
                sb.Append("...(").Append(Chronotopes.Count).Append(')');
        }

        return sb.ToString();
    }
}
