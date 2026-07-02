using System;
using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Decorated counts part. This contains a set of counts.
/// <para>Tag: <c>it.vedph.decorated-counts</c>.</para>
/// </summary>
[Tag("it.vedph.decorated-counts")]
public sealed class DecoratedCountsPart : PartBase
{
    /// <summary>
    /// Gets or sets the counts.
    /// </summary>
    public List<DecoratedCount> Counts { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="DecoratedCountsPart"/> class.
    /// </summary>
    public DecoratedCountsPart()
    {
        Counts = [];
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>ID-min</c> and <c>ID-max</c> for each count ID.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();

        builder.Set("tot", Counts?.Count ?? 0, false);

        if (Counts?.Count > 0)
        {
            // collect all the min-max values for each measurement name
            Dictionary<string, Tuple<int, int>> minMax = [];
            foreach (DecoratedCount count in Counts)
            {
                if (minMax.TryGetValue(count.Id!,
                    out Tuple<int, int>? tuple))
                {
                    if (tuple == null)
                    {
                        tuple = new Tuple<int, int>(
                            count.Value, count.Value);
                    }
                    else
                    {
                        if (count.Value < tuple.Item1 ||
                            count.Value > tuple.Item2)
                        {
                            tuple = new Tuple<int, int>(
                                Math.Min(tuple.Item1, count.Value),
                                Math.Max(tuple.Item2, count.Value));
                        }
                    }
                    minMax[count.Id!] = tuple;
                }
                else
                {
                    minMax[count.Id!] = new Tuple<int, int>(
                        count.Value, count.Value);
                }
            }

            foreach (KeyValuePair<string, Tuple<int, int>> kvp in minMax)
            {
                builder.AddValue(kvp.Key + "-min", kvp.Value.Item1);
                builder.AddValue(kvp.Key + "-max", kvp.Value.Item2);
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
            new DataPinDefinition(DataPinValueType.Decimal,
                "<ID>-min",
                "The minimum value for the specified count ID."),
            new DataPinDefinition(DataPinValueType.Decimal,
                "<ID>-max",
                "The maximum value for the specified count ID.")
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

        sb.Append("[DecoratedCounts]");

        if (Counts?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Counts)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Counts.Count > 3)
                sb.Append("...(").Append(Counts.Count).Append(')');
        }

        return sb.ToString();
    }
}
