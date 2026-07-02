using System;
using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Generic metadata part. This contains a set of name=value pairs.
/// <para>Tag: <c>it.vedph.metadata</c>.</para>
/// </summary>
[Tag("it.vedph.metadata")]
public sealed class MetadataPart : PartBase
{
    /// <summary>
    /// Gets or sets the metadata.
    /// </summary>
    public List<Metadatum> Metadata { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MetadataPart"/> class.
    /// </summary>
    public MetadataPart()
    {
        Metadata = new List<Metadatum>();
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins
    /// directly derived from metadata.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();

        builder.Set("tot", Metadata?.Count ?? 0, false);

        if (Metadata?.Count > 0)
        {
            foreach (Metadatum metadatum in Metadata)
                builder.AddValue(metadatum.Name, metadatum.Value);
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
               "<NAME>",
               "The metadata values."),
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

        sb.Append("[Metadata]");

        if (Metadata?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Metadata)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Metadata.Count > 3)
                sb.Append("...(").Append(Metadata.Count).Append(')');
        }

        return sb.ToString();
    }
}
