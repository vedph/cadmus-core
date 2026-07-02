using System.Collections.Generic;
using System.Text;
using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;

namespace Cadmus.General.Parts;

/// <summary>
/// Names part. This contains any number of names assigned to the item.
/// <para>Tag: <c>it.vedph.names</c>.</para>
/// </summary>
[Tag("it.vedph.names")]
public sealed class NamesPart : PartBase
{
    /// <summary>
    /// Gets or sets the names.
    /// </summary>
    public List<AssertedProperName> Names { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NamesPart"/> class.
    /// </summary>
    public NamesPart()
    {
        Names = new List<AssertedProperName>();
    }

    /// <summary>
    /// Get all the key=value pairs (pins) exposed by the implementor.
    /// </summary>
    /// <param name="item">The optional item. The item with its parts
    /// can optionally be passed to this method for those parts requiring
    /// to access further data.</param>
    /// <returns>The pins: <c>tot-count</c> and a collection of pins with
    /// these keys: <c>name</c>.</returns>
    public override IEnumerable<DataPin> GetDataPins(IItem? item = null)
    {
        DataPinBuilder builder = new(DataPinHelper.DefaultFilter);

        builder.Set("tot", Names?.Count ?? 0, false);

        if (Names?.Count > 0)
        {
            foreach (AssertedProperName name in Names)
            {
                string n = name.GetFullName();
                if (!string.IsNullOrEmpty(n))
                    builder.AddValue("name", n, filter: true);
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
               "The total count of names."),
            new DataPinDefinition(DataPinValueType.String,
               "name",
               "The filtered names."),
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

        sb.Append("[Names]");

        if (Names?.Count > 0)
        {
            sb.Append(' ');
            int n = 0;
            foreach (var entry in Names)
            {
                if (++n > 3) break;
                if (n > 1) sb.Append("; ");
                sb.Append(entry);
            }
            if (Names.Count > 3)
                sb.Append("...(").Append(Names.Count).Append(')');
        }

        return sb.ToString();
    }
}
