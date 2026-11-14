using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;

namespace Cadmus.Import.Excel;

/// <summary>
/// External link column entry region parser. The cell contains one or more
/// identifiers of external resources (e.g. ISTC IDs), each added as an
/// external link with the configured scope to the item's
/// <see cref="PinLinksPart"/>. The link's target GID is the identifier,
/// optionally prefixed with <see cref="ColExternalLinkEntryRegionParserOptions.
/// GidPrefix"/>, and its label is the identifier itself.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-ext-link</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-ext-link")]
public sealed class ColExternalLinkEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColExternalLinkEntryRegionParserOptions>
{
    private ColExternalLinkEntryRegionParserOptions? _options;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColExternalLinkEntryRegionParserOptions options)
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

        IList<string> ids = _options.Separator != null
            ? ImportHelper.GetValueList(value, false, [_options.Separator.Value])
            : [value];
        if (ids.Count == 0) return;

        PinLinksPart part = context.EnsurePartForCurrentItem<PinLinksPart>(
            _options.PartRoleId);
        foreach (string id in ids)
        {
            part.Links.Add(new AssertedCompositeId
            {
                Scope = _options.Scope,
                Tag = _options.LinkTag,
                Target = new PinTarget
                {
                    Gid = (_options.GidPrefix ?? "") + id,
                    Label = id
                }
            });
        }
    }
}

/// <summary>
/// Options for <see cref="ColExternalLinkEntryRegionParser"/>.
/// </summary>
public class ColExternalLinkEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the scope of the links (e.g. <c>istc</c>).
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets the optional tag of the links.
    /// </summary>
    public string? LinkTag { get; set; }

    /// <summary>
    /// Gets or sets the optional prefix prepended to each identifier to build
    /// the link target GID (e.g. a base URI).
    /// </summary>
    public string? GidPrefix { get; set; }

    /// <summary>
    /// Gets or sets the optional separator for multiple values in the cell.
    /// When null, the cell has a single value.
    /// </summary>
    public char? Separator { get; set; }

    /// <summary>
    /// Gets or sets the optional role ID of the target links part.
    /// </summary>
    public string? PartRoleId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is required.
    /// </summary>
    public bool IsRequired { get; set; }
}
