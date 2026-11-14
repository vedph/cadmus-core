using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;

namespace Cadmus.Import.Excel;

/// <summary>
/// Boolean flag columns entry region parser. Each handled column is
/// mapped to a thesaurus entry ID: when its cell contains <c>YES</c>, the
/// entry ID is added to the item's <see cref="CategoriesPart"/> with the
/// configured role. Empty cells and <c>NO</c> are ignored, while any other
/// value is ignored with a warning.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-flags</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-flags")]
public sealed class ColFlagsEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColFlagsEntryRegionParserOptions>
{
    private ColFlagsEntryRegionParserOptions _options = new();

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => [.. _options.Flags.Keys];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColFlagsEntryRegionParserOptions options)
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
        if (!ImportHelper.IsYes(value))
        {
            if (value != null &&
                !string.Equals(value, "no", StringComparison.OrdinalIgnoreCase))
            {
                Logger?.LogWarning("Unexpected value \"{Value}\" for flag " +
                    "at region {Region}", value, region);
            }
            return;
        }

        string id = _options.Flags[region.Tag!];
        if (_options.ThesaurusId != null)
        {
            ImportHelper.ValidateThesaurusEntryId(context, _options.ThesaurusId,
                id, Logger);
        }

        CategoriesPart part = context.EnsurePartForCurrentItem<CategoriesPart>(
            _options.PartRoleId);
        if (!part.Categories.Contains(id)) part.Categories.Add(id);
    }
}

/// <summary>
/// Options for <see cref="ColFlagsEntryRegionParser"/>.
/// </summary>
public class ColFlagsEntryRegionParserOptions : DisabledOptions
{
    /// <summary>
    /// Gets or sets the flags, where each key is a column region tag (e.g.
    /// <c>col-cults</c>) and each value is the corresponding thesaurus entry
    /// ID (e.g. <c>cults</c>).
    /// </summary>
    public Dictionary<string, string> Flags { get; set; } = [];

    /// <summary>
    /// Gets or sets the optional ID of the thesaurus used to validate entry
    /// IDs (e.g. <c>categories_site-feats@en</c>).
    /// </summary>
    public string? ThesaurusId { get; set; }

    /// <summary>
    /// Gets or sets the optional role ID of the target categories part.
    /// </summary>
    public string? PartRoleId { get; set; }
}
