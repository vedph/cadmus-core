using Cadmus.Core;
using Cadmus.Import.Proteus;
using Microsoft.Extensions.Logging;
using Proteus.Core.Entries;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace Cadmus.Import.Excel;

/// <summary>
/// Represents an abstract parser for entry regions tagged "row" that
/// initializes a target Item with a configurable facet for the first row
/// encountered.
/// </summary>
/// <remarks>Implementors must set the FacetId. The parser tracks entry into
/// a "row" region and, for the first row only, resets the entry set context,
/// locates a "row-start" command, logs the row Y coordinate, and adds a new
/// Item with the FacetId. An InvalidOperationException is thrown if the
/// "row-start" command is not found.</remarks>
public abstract class RowEntryRegionParser : EntryRegionParser
{
    private bool _first = false;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => ["row"];

    /// <summary>
    /// The target item facet ID. Set once by implementors.
    /// </summary>
    protected string FacetId { get; set; } = "";

    /// <summary>
    /// Invoked by the pipeline when the current region has changed. This
    /// tracks entering a row, so that its item is created only once.
    /// </summary>
    /// <param name="tag">The region tag.</param>
    /// <param name="index">The index of the entry opening the new region.
    /// </param>
    /// <param name="entered">True if the region was entered, false if it
    /// was exited.</param>
    /// <param name="context">The context.</param>
    public override async Task RegionChangedAsync(string tag, int index,
        bool entered, IEntrySetContext context)
    {
        await base.RegionChangedAsync(tag, index, entered, context);

        if (tag == "row" && entered)
        {
            _first = true;
        }
    }

    /// <summary>
    /// Parses the row region: for the first row entered, this resets the
    /// context and adds to it a new item with <see cref="FacetId"/>.
    /// </summary>
    /// <param name="entrySet">The entries set.</param>
    /// <param name="entryIndex">The index of the first entry to parse.</param>
    /// <param name="entryRegions">The regions.</param>
    /// <param name="entryRegionIndex">Index of the region in the set.</param>
    /// <returns>The index to the next entry to be parsed.</returns>
    /// <exception cref="InvalidOperationException">row-start command not
    /// found.</exception>
    protected int ParseRow(EntrySet entrySet, int entryIndex,
        IReadOnlyList<EntryRegion> entryRegions, int entryRegionIndex)
    {
        // create the target item only for the first row
        if (!_first) return entryIndex;

        entrySet.Context.Reset();

        // find the first row-start command
        DecodedCommandEntry? row = null;
        EntryRegion region = entryRegions[entryRegionIndex];
        for (int i = region.Range.Start.Entry; i <= region.Range.End.Entry; i++)
        {
            if (entrySet.Entries[i] is DecodedCommandEntry cmd &&
                cmd.Name == "row-start")
            {
                row = cmd;
                break;
            }
        }
        if (row == null)
        {
            Logger?.LogError("Row command not found in region {Region}",
                region);
            throw new InvalidOperationException(
                "Row command not found in region " + region);
        }

        // log row's Y
        int y = int.Parse(row.GetArgument("y")!, CultureInfo.InvariantCulture);
        Logger?.LogInformation("-- ROW: {Row}", y);

        // add item for the row
        Item item = new()
        {
            FacetId = FacetId,
            CreatorId = "zeus",
            UserId = "zeus"
        };
        CadmusEntrySetContext ctx = (CadmusEntrySetContext)entrySet.Context;
        ctx.Items.Clear();
        ctx.Items.Add(item);

        _first = false;
        return entryIndex + 1;
    }
}
