using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using System.Collections.Generic;
using System.Text;

namespace Cadmus.General.Parts;

/// <summary>
/// District-based location part.
/// This is a single place name using a hierarchy of district components,
/// like e.g. province, city, and location. Each component can draw its data
/// from a closed list, or allow free text.
/// Levels in the districts hierarchy are provided via a hierarchical-like
/// thesaurus (<c>district-name-piece-types@en</c>), where each component is
/// defined by a simple ID with any number of children entries, each with a
/// composite ID. In it, the asterisk suffix means a parent entry, while entries
/// with children have their values composed by the parent entry ID without
/// the asterisk, plus dot and another ID, like <c>a.cr</c>. Finally, the
/// <c>_order</c> entry is used to define the hierarchical order of components.
/// <para>For instance, an entry with ID <c>p*</c> and value <c>province</c>
/// can be followed by "child" entries like <c>p.rm</c>=Rome,
/// <c>p.fi</c>=Florence, etc.; while an entry like <c>c*</c> without children
/// allows for a free text. In this example, <c>_order</c> would be
/// <c>p c</c>.</para>
/// <para>Tag: <c>it.vedph.district-location</c>.</para>
/// </summary>
[Tag("it.vedph.district-location")]
public sealed class DistrictLocationPart : PartBase
{
    /// <summary>
    /// Gets or sets the place name.
    /// </summary>
    public ProperName? Place { get; set; }

    /// <summary>
    /// Gets or sets a note about the place.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new(DataPinHelper.DefaultFilter);

        if (Place != null)
            builder.AddValue("place", Place.GetFullName(), filter: true);

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
                "place",
                "The district-based place.",
                "f"),
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

        sb.Append("[DistrictLocation]");

        if (Place != null) sb.Append(' ').Append(Place.GetFullName());

        return sb.ToString();
    }
}
