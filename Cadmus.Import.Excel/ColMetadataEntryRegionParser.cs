using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Import.Excel;

/// <summary>
/// Column metadata entry region parser. This adds the cell's value as
/// a metadatum to the item's <see cref="MetadataPart"/>, and/or sets it
/// as the item's title and/or group ID. When a separator is configured, the
/// cell can contain multiple values, each becoming a distinct metadatum.
/// <para>When the metadatum is the item's EID and
/// <see cref="ColMetadataEntryRegionParserOptions.ReuseIdsFrom"/> is set,
/// if an item with the same EID is found in that item IDs set, its item and
/// metadata part IDs are reused. This way, a first import pass (e.g. a dump)
/// can export the item IDs, which are then reused by the next pass, so that
/// items can link to items imported later in the same pass.</para>
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-metadata</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-metadata")]
public sealed class ColMetadataEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColMetadataEntryRegionParserOptions>
{
    private ColMetadataEntryRegionParserOptions? _options;
    private bool _missingSetLogged;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColMetadataEntryRegionParserOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
    }

    private void ReuseIds(CadmusEntrySetContext context, MetadataPart part,
        string eid)
    {
        ExportedItemIdSet? set = ImportHelper.TryGetItemIdsSet(context,
            _options!.ReuseIdsFrom);
        if (set == null)
        {
            if (!_missingSetLogged)
            {
                Logger?.LogWarning("Item IDs set {SetName} not loaded: " +
                    "new IDs will be generated", _options.ReuseIdsFrom);
                _missingSetLogged = true;
            }
            return;
        }

        ExportedItemId? id = ImportHelper.FindItemId(set, eid, Logger);
        if (id == null)
        {
            Logger?.LogWarning("EID {Eid} not found in item IDs set " +
                "{SetName}: new IDs will be generated", eid, _options.ReuseIdsFrom);
            return;
        }

        ImportHelper.SetItemId(context.CurrentItem!, id.Id);
        if (id.MetadataPartId != null) part.Id = id.MetadataPartId;
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

        if (_options.SetTitle) context.CurrentItem!.Title = value;
        if (_options.SetGroupId) context.CurrentItem!.GroupId = value;

        if (string.IsNullOrEmpty(_options.Name)) return;

        IList<string> values = _options.Separator != null
            ? ImportHelper.GetValueList(value, false, [_options.Separator.Value])
            : [value];
        if (values.Count == 0) return;

        MetadataPart part = context.EnsurePartForCurrentItem<MetadataPart>();
        foreach (string v in values)
        {
            part.Metadata.Add(new Metadatum
            {
                Type = _options.Type,
                Name = _options.Name,
                Value = v
            });
        }

        // reuse IDs only for the first EID
        if (!string.IsNullOrEmpty(_options.ReuseIdsFrom) &&
            _options.Name == "eid" &&
            part.Metadata.Count(m => m.Name == "eid") == 1)
        {
            ReuseIds(context, part, values[0]);
        }
    }
}

/// <summary>
/// Options for <see cref="ColMetadataEntryRegionParser"/>.
/// </summary>
public class ColMetadataEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the metadatum name (e.g. <c>eid</c>). When not set, no
    /// metadatum is added.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Gets or sets the metadatum type. Default is <c>string</c>.
    /// </summary>
    public string? Type { get; set; } = "string";

    /// <summary>
    /// Gets or sets the optional separator used when the cell can contain
    /// multiple values, each becoming a distinct metadatum. When null, the
    /// cell has a single value.
    /// </summary>
    public char? Separator { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value must also be set as
    /// the item's title.
    /// </summary>
    public bool SetTitle { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the value must also be set as
    /// the item's group ID.
    /// </summary>
    public bool SetGroupId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is required.
    /// </summary>
    public bool IsRequired { get; set; }

    /// <summary>
    /// Gets or sets the name of the item IDs set to reuse IDs from when
    /// <see cref="Name"/> is <c>eid</c>. This is the name of the file the set
    /// was loaded from, without its extension (e.g. <c>item-ids-c</c>).
    /// </summary>
    public string? ReuseIdsFrom { get; set; }
}
