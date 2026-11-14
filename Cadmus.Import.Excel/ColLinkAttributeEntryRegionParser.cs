using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;
using System.Linq;

namespace Cadmus.Import.Excel;

/// <summary>
/// Link attribute column entry region parser. This sets an attribute
/// (rank, note, or tag) of the links added to the item's
/// <see cref="PinLinksPart"/> with the configured scope by a previous column
/// of the same row. For instance, a certainty column can provide the rank
/// of the link built from the column preceding it.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-link-attr</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-link-attr")]
public sealed class ColLinkAttributeEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColLinkAttributeEntryRegionParserOptions>
{
    private ColLinkAttributeEntryRegionParserOptions? _options;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColLinkAttributeEntryRegionParserOptions options)
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
        if (value == null) return;

        AssertedCompositeId[] links = context.CurrentItem!.Parts
            .OfType<PinLinksPart>()
            .SelectMany(p => p.Links)
            .Where(l => l.Scope == _options!.Scope)
            .ToArray();

        if (links.Length == 0)
        {
            Logger?.LogWarning("{Tag} value \"{Value}\" without any link " +
                "with scope {Scope} at region {Region}",
                region.Tag, value, _options!.Scope, region);
            return;
        }

        short rank = _options!.Attribute == LinkAttribute.Rank
            ? ImportHelper.LabelToRank(value, Logger)
            : (short)0;

        foreach (AssertedCompositeId link in links)
        {
            switch (_options.Attribute)
            {
                case LinkAttribute.Rank:
                    ImportHelper.SetRank(link, rank);
                    break;
                case LinkAttribute.Note:
                    link.Note = value;
                    break;
                case LinkAttribute.Tag:
                    link.Tag = value;
                    break;
            }
        }
    }
}

/// <summary>
/// Link attribute set by <see cref="ColLinkAttributeEntryRegionParser"/>.
/// </summary>
public enum LinkAttribute
{
    /// <summary>
    /// The assertion rank (from <c>high</c>, <c>medium</c>, <c>low</c>).
    /// </summary>
    Rank = 0,

    /// <summary>
    /// The link note.
    /// </summary>
    Note,

    /// <summary>
    /// The link tag.
    /// </summary>
    Tag
}

/// <summary>
/// Options for <see cref="ColLinkAttributeEntryRegionParser"/>.
/// </summary>
public class ColLinkAttributeEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the scope of the target links.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the link attribute to set.
    /// </summary>
    public LinkAttribute Attribute { get; set; }
}
