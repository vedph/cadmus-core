using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;

namespace Cadmus.Import.Excel;

/// <summary>
/// Internal link column entry region parser. The column contains the
/// EID of the target item, which is resolved via the loaded item IDs set
/// into an internal link added to the item's <see cref="PinLinksPart"/>.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-internal-link</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-internal-link")]
public sealed class ColInternalLinkEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColInternalLinkEntryRegionParserOptions>
{
    private ColInternalLinkEntryRegionParserOptions? _options;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Applies the parser options for internal link entry regions.
    /// </summary>
    /// <param name="options">Options used to configure parser behavior.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColInternalLinkEntryRegionParserOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
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
        if (_options!.IsRequired && !CheckRequired(region, value)) return;
        if (value == null) return;

        ExportedItemIdSet? set = ImportHelper.TryGetItemIdsSet(context,
            _options.ItemIdsSet);
        if (set == null)
        {
            Logger?.LogError("No item IDs set {SetName} to resolve {Eid} " +
                "at region {Region}", _options.ItemIdsSet ?? "*", value, region);
            return;
        }

        PinLinksPart part = context.EnsurePartForCurrentItem<PinLinksPart>();
        if (ImportHelper.AddInternalLink(part, value, set, Logger,
            _options.Scope) == null)
        {
            Logger?.LogError("Unresolved link target {Eid} at region {Region}",
                value, region);
        }
    }
}

/// <summary>
/// Options for <see cref="ColInternalLinkEntryRegionParser"/>.
/// </summary>
public class ColInternalLinkEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the name of the item IDs set to look up, i.e. the name of
    /// the file it was loaded from without extension (e.g. <c>item-ids-s</c>).
    /// When null, the merged set of all the loaded sets is used.
    /// </summary>
    public string? ItemIdsSet { get; set; }

    /// <summary>
    /// Gets or sets the optional scope of the link (e.g. <c>site</c>).
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is required.
    /// </summary>
    public bool IsRequired { get; set; }
}
