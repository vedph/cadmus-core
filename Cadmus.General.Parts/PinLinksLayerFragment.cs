using System.Collections.Generic;
using System.Text;
using Cadmus.Core.Layers;
using Cadmus.Core;
using Fusi.Tools.Configuration;
using System.Linq;
using Cadmus.Refs.Bricks;

namespace Cadmus.General.Parts;

/// <summary>
/// Pin-based links layer fragment. This fragment links a portion of the base
/// text to any items via their parts pins.
/// Tag: <c>fr.it.vedph.pin-links</c>.
/// </summary>
/// <seealso cref="ITextLayerFragment" />
[Tag("fr.it.vedph.pin-links")]
public sealed class PinLinksLayerFragment : ITextLayerFragment
{
    /// <summary>
    /// Gets or sets the location of this fragment.
    /// </summary>
    /// <remarks>
    /// The location can be expressed in different ways according to the
    /// text coordinates system being adopted. For instance, it might be a
    /// simple token-based coordinates system (e.g. 1.2=second token of
    /// first block), or a more complex system like an XPath expression.
    /// </remarks>
    public string Location { get; set; }

    /// <summary>
    /// Gets or sets the entries.
    /// </summary>
    public List<AssertedCompositeId> Links { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PinLinksLayerFragment"/>
    /// class.
    /// </summary>
    public PinLinksLayerFragment()
    {
        Location = "";
        Links = new List<AssertedCompositeId>();
    }

    /// <summary>
    /// Get all the key=value pairs exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>fr.tag</c>=tag if any.</returns>
    public IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new(new StandardDataPinTextFilter());

        builder.Set(PartBase.FR_PREFIX + "tot", Links?.Count ?? 0, false);

        if (Links?.Count > 0)
        {
            foreach (PinTarget? target in Links.Where(l => l.Target != null)
                .Select(l => l.Target))
            {
                builder.AddValue(PartBase.FR_PREFIX + "gid", target?.Gid);
                builder.AddValue(PartBase.FR_PREFIX + "label",
                    target?.Label, filter: true, filterOptions: true);
                builder.AddValue(PartBase.FR_PREFIX + "item-id", target?.ItemId);
                //if (!string.IsNullOrEmpty(link.Tag))
                //{
                //    builder.AddValue(PartBase.FR_PREFIX + "tagged-item-id",
                //        $"{link.Tag} {link.ItemId}");
                //}
            }
        }

        return builder.Build(null);
    }

    /// <summary>
    /// Gets the definitions of data pins used by the implementor.
    /// </summary>
    /// <returns>Data pins definitions.</returns>
    public IList<DataPinDefinition> GetDataPinDefinitions()
    {
        return new List<DataPinDefinition>(new[]
        {
            new DataPinDefinition(DataPinValueType.Integer,
               PartBase.FR_PREFIX + "tot-count",
               "The total count of entries."),
            new DataPinDefinition(DataPinValueType.String,
               PartBase.FR_PREFIX + "gid",
               "The target global ID.",
               "M"),
            new DataPinDefinition(DataPinValueType.String,
               PartBase.FR_PREFIX + "label",
               "The target label.",
               "MF"),
            new DataPinDefinition(DataPinValueType.String,
               PartBase.FR_PREFIX + "item-id",
               "The target item IDs.",
               "M"),
            //new DataPinDefinition(DataPinValueType.String,
            //   PartBase.FR_PREFIX + "tagged-item-id",
            //   "The target item IDs prefixed by tag and space."),
        });
    }

    /// <summary>
    /// Returns a <see cref="string" /> that represents this instance.
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