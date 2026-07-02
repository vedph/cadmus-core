using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using System.Collections.Generic;
using System.Text;

namespace Cadmus.General.Parts;

/// <summary>
/// Asserted historical dates part.
/// Tag: <c>it.vedph.asserted-historical-dates</c>.
/// </summary>
[Tag("it.vedph.asserted-historical-dates")]
public sealed class AssertedHistoricalDatesPart : PartBase
{
    /// <summary>
    /// Gets or sets the dates.
    /// </summary>
    public List<AssertedHistoricalDate> Dates { get; set; } = [];

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();

        builder.Set("tot", Dates?.Count ?? 0, false);

        if (Dates?.Count > 0)
        {
            foreach (AssertedHistoricalDate date in Dates)
            {
                builder.AddValue("date-value", date.GetSortValue());
                if (!string.IsNullOrEmpty(date.Tag))
                {
                    builder.AddValue($"date-{date.Tag}-value",
                        date.GetSortValue());
                }
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
        return
        [
            new DataPinDefinition(DataPinValueType.Integer,
               "tot-count",
               "The total count of entries."),
            new DataPinDefinition(DataPinValueType.Decimal,
                "date-value",
                "The sortable date value (0 if undefined).",
                "M"),
            new DataPinDefinition(DataPinValueType.Decimal,
                "date-<TAG>-value",
                "The tagged sortable date value (0 if undefined).",
                "M"),
        ];
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

        sb.Append("[AssertedHistoricalDates]");

        if (Dates?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Dates)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Dates.Count > 3)
                sb.Append("...(").Append(Dates.Count).Append(')');
        }

        return sb.ToString();
    }
}