using Cadmus.Core;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;

namespace Cadmus.Import.Excel;

/// <summary>
/// Column parent item ID entry region parser. This column contains the EID
/// of the parent item of the row being imported, which was imported in a
/// previous pass. This EID is used to fetch the corresponding item ID from the
/// loaded item IDs set, so that the item being created for this row (and all
/// its parts) gets the same ID and title of the parent item. When exporting
/// items into the database with merging enabled, this will merge the parts of
/// the row item into the parent item.
/// <para>If the parent is not found, an error is logged and the row is
/// skipped.</para>
/// <para>As merging replaces parts with the same type and role, when several
/// rows target the same parent you may need to accumulate their parts: to
/// this end, derive from this class and override
/// <see cref="OnParentItemResolved"/>.</para>
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-parent-item-id</c>.
/// </para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-parent-item-id")]
public class ColParentItemIdEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColParentItemIdEntryRegionParserOptions>
{
    /// <summary>
    /// Gets the options.
    /// </summary>
    protected ColParentItemIdEntryRegionParserOptions? Options
    { get; private set; }

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => Options?.Tag != null ? [Options.Tag] : [];

    /// <summary>
    /// Configure this parser.
    /// </summary>
    /// <param name="options">Options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColParentItemIdEntryRegionParserOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
    }

    /// <summary>
    /// Called after the current item got the ID and title of its parent
    /// item. The default implementation does nothing.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="item">The current item.</param>
    protected virtual void OnParentItemResolved(CadmusEntrySetContext context,
        IItem item)
    {
    }

    /// <summary>
    /// Parses the specified column value.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="region">The column region.</param>
    /// <param name="value">The filtered value, or null if empty.</param>
    protected override void ParseValue(CadmusEntrySetContext context,
        EntryRegion region, string? value)
    {
        // there must be a value, else it's an error
        if (!CheckRequired(region, value))
        {
            ImportHelper.SkipRow(context);
            return;
        }

        ExportedItemIdSet? set = ImportHelper.TryGetItemIdsSet(context,
            Options?.ItemIdsSet);
        ExportedItemId? id = set != null
            ? ImportHelper.FindItemId(set, value!, Logger)
            : null;

        if (id == null)
        {
            Logger?.LogError("{Tag} column with unknown EID {Eid} at region " +
                "{Region}: row skipped", region.Tag, value, region);
            ImportHelper.SkipRow(context);
            return;
        }

        // set the parent item ID and title
        IItem item = context.CurrentItem!;
        ImportHelper.SetItemId(item, id.Id);
        item.Title = id.Title;
        OnParentItemResolved(context, item);
    }
}

/// <summary>
/// Options for <see cref="ColParentItemIdEntryRegionParser"/>.
/// </summary>
public class ColParentItemIdEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the name of the item IDs set to look up, i.e. the name of
    /// the file it was loaded from without extension (e.g. <c>item-ids-s</c>).
    /// When null, the merged set of all the loaded sets is used.
    /// </summary>
    public string? ItemIdsSet { get; set; }
}
