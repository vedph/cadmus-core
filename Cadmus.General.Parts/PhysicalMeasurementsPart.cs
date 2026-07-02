using System;
using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Cadmus.Mat.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Physical measurements part.
/// <para>Tag: <c>it.vedph.physical-measurements</c>.</para>
/// </summary>
[Tag("it.vedph.physical-measurements")]
public sealed class PhysicalMeasurementsPart : PartBase
{
    /// <summary>
    /// Gets or sets the measurements.
    /// </summary>
    public List<PhysicalMeasurement> Measurements { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PhysicalMeasurementsPart"/> class.
    /// </summary>
    public PhysicalMeasurementsPart()
    {
        Measurements = [];
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>name-min</c> and <c>name-max</c> for each measurement
    /// name.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();

        builder.Set("tot", Measurements?.Count ?? 0, false);

        if (Measurements?.Count > 0)
        {
            // collect all the min-max values for each measurement name
            Dictionary<string, Tuple<float, float>> minMax = [];
            foreach (PhysicalMeasurement measurement in Measurements)
            {
                if (minMax.TryGetValue(measurement.Name,
                    out Tuple<float, float>? tuple))
                {
                    if (tuple == null)
                    {
                        tuple = new Tuple<float, float>(
                            measurement.Value, measurement.Value);
                    }
                    else
                    {
                        if (measurement.Value < tuple.Item1 ||
                            measurement.Value > tuple.Item2)
                        {
                            tuple = new Tuple<float, float>(
                                Math.Min(tuple.Item1, measurement.Value),
                                Math.Max(tuple.Item2, measurement.Value));
                        }
                    }
                    minMax[measurement.Name] = tuple;
                }
                else
                {
                    minMax[measurement.Name] = new Tuple<float, float>(
                        measurement.Value, measurement.Value);
                }
            }

            foreach (KeyValuePair<string, Tuple<float, float>> kvp in minMax)
            {
                builder.AddValue(kvp.Key + "-min", (double)kvp.Value.Item1);
                builder.AddValue(kvp.Key + "-max", (double)kvp.Value.Item2);
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
                "<NAME>-min",
                "The minimum value for the specified measurement name."),
            new DataPinDefinition(DataPinValueType.Decimal,
                "<NAME>-max",
                "The maximum value for the specified measurement name.")
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

        sb.Append("[PhysicalMeasurements]");

        if (Measurements?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Measurements)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Measurements.Count > 3)
                sb.Append("...(").Append(Measurements.Count).Append(')');
        }

        return sb.ToString();
    }
}
