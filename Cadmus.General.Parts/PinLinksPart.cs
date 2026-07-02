using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Pin-based links part. This part is used to collect any number of
/// pin-based dynamic lookup references, so that you can easily connect
/// an item to one or more items via pins targeting any of its parts.
/// <para>Tag: <c>it.vedph.pin-links</c>.</para>
/// </summary>
[Tag("it.vedph.pin-links")]
public sealed class PinLinksPart : PartBase
{
    /// <summary>
    /// Gets or sets the entries.
    /// </summary>
    public List<AssertedCompositeId> Links { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinLinksPart"/> class.
    /// </summary>
    public PinLinksPart()
    {
        Links = new List<AssertedCompositeId>();
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>item-id</c> = target item ID.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new(new StandardDataPinTextFilter());

        builder.Set("tot", Links?.Count ?? 0, false);

        if (Links?.Count > 0)
        {
            foreach (PinTarget? target in Links.Where(l => l.Target != null)
                .Select(l => l.Target))
            {
                builder.AddValue("gid", target?.Gid);
                builder.AddValue("label",
                    target?.Label, filter: true, filterOptions: true);
                builder.AddValue("item-id", target?.ItemId);
                //if (!string.IsNullOrEmpty(link.Tag))
                //{
                //    builder.AddValue("tagged-item-id",
                //        $"{link.Tag} {link.ItemId}");
                //}
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
               "gid",
               "The target global ID.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               "label",
               "The target label.",
               "MF"),
            new DataPinDefinition(DataPinValueType.String,
               "item-id",
               "The target item IDs.",
               "M"),
            //new DataPinDefinition(DataPinValueType.String,
            //   "tagged-item-id",
            //   "The target item IDs prefixed by tag and space.",
            //   "M")
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

        sb.Append("[PinLinks]");

        if (Links?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Links)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Links.Count > 3)
                sb.Append("...(").Append(Links.Count).Append(')');
        }

        return sb.ToString();
    }
}
