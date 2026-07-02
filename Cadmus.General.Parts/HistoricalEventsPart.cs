using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Generic historical events part.
/// <para>Tag: <c>it.vedph.historical-events</c>.</para>
/// </summary>
[Tag("it.vedph.historical-events")]
public sealed class HistoricalEventsPart : PartBase
{
    /// <summary>
    /// Gets or sets the entries.
    /// </summary>
    public List<HistoricalEvent> Events { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoricalEventsPart"/> class.
    /// </summary>
    public HistoricalEventsPart()
    {
        Events = new List<HistoricalEvent>();
    }

    private static void AddChronotopePins(HistoricalEvent entry,
        DataPinBuilder builder)
    {
        foreach (AssertedChronotope c in entry.Chronotopes)
        {
            if (c.Place?.Value != null)
            {
                builder.AddValue("place", c.Place.Value);

                if (entry.Eid != null)
                {
                    builder.AddValue("hasPlace@" + entry.Eid,
                        c.Place.Value);
                }
            }

            if (c.Date is not null)
            {
                double sortValue = c.Date.GetSortValue();
                builder.AddValue("date-value", sortValue);

                if (entry.Eid != null)
                {
                    builder.AddValue("hasDate@" + entry.Eid, c.Date.ToString());
                    builder.AddValue("hasDateValue@" + entry.Eid,
                        sortValue);
                }
            }
        }
    }

    private static void AddRelatedEntriesPins(HistoricalEvent entry,
        DataPinBuilder builder)
    {
        foreach (RelatedEntity entity in entry.RelatedEntities)
        {
            builder.AddValue("eid2@" + entry.Eid, entity.Id?.Target?.Gid);
            builder.AddValue($"rel@{entry.Eid}@{entity.Id?.Target?.Gid}",
                entity.Relation);
        }
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>eid</c>, <c>type</c>, <c>place</c>, <c>date-value</c>.
    /// </returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new();

        builder.Set("tot", Events?.Count ?? 0, false);

        if (Events?.Count > 0)
        {
            foreach (HistoricalEvent entry in Events)
            {
                builder.AddValue("eid", entry.Eid);
                builder.AddValue("type", entry.Type);

                if (entry.Chronotopes?.Count > 0)
                    AddChronotopePins(entry, builder);

                // event's type
                if (entry.Eid != null)
                {
                    if (!string.IsNullOrEmpty(entry.Type))
                        builder.AddValue("type@" + entry.Eid, entry.Type);
                    if (!string.IsNullOrEmpty(entry.Tag))
                        builder.AddValue("tag@" + entry.Eid, entry.Tag);
                }

                // related entries
                if (entry.RelatedEntities?.Count > 0)
                    AddRelatedEntriesPins(entry, builder);
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
               "The total count of events."),
            new DataPinDefinition(DataPinValueType.String,
               "eid",
               "The events IDs.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "type",
               "The events types.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "place",
               "The events places.",
               "M"),
            new DataPinDefinition(DataPinValueType.Decimal,
               "date-value",
               "The events date values.",
               "M"),
            // semantic
            new DataPinDefinition(DataPinValueType.String,
               "type@EID",
               "The event's type.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "tag@EID",
               "The event's tag.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "hasPlace@EID",
               "The event's place.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "hasDate@EID",
               "The event's date.",
               "M"),
            new DataPinDefinition(DataPinValueType.Decimal,
               "hasDateValue@EID",
               "The event's date value.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "eid2@EID",
               "The event's related entity ID.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "rel@EID@RELID",
               "The link between the event and its related entity.",
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

        sb.Append("[Events]");

        if (Events?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Events)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Events.Count > 3)
                sb.Append("...(").Append(Events.Count).Append(')');
        }

        return sb.ToString();
    }
}
