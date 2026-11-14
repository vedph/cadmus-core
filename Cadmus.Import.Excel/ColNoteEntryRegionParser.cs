using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Proteus.Core.Regions;
using System;

namespace Cadmus.Import.Excel;

/// <summary>
/// Column note entry region parser. This appends the cell's text to the
/// text of the <see cref="NotePart"/> with the configured role, optionally
/// under a heading. The cell's text is just trimmed, so that its line breaks
/// are preserved; empty or whitespace-only cells are ignored.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-note</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-note")]
public sealed class ColNoteEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColNoteEntryRegionParserOptions>
{
    private ColNoteEntryRegionParserOptions? _options;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Gets a value indicating whether the value passed to
    /// <see cref="ParseValue" /> must be just trimmed, rather than filtered.
    /// This preserves its line breaks and inner whitespaces.
    /// </summary>
    protected override bool RawValue => true;

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColNoteEntryRegionParserOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
    }

    /// <summary>
    /// Parses the specified column value.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="region">The column region.</param>
    /// <param name="value">The value, or null if empty.</param>
    protected override void ParseValue(CadmusEntrySetContext context,
        EntryRegion region, string? value)
    {
        if (value == null) return;

        NotePart part = context.EnsurePartForCurrentItem<NotePart>(
            _options!.PartRoleId);
        part.Text = ImportHelper.AppendToNote(part.Text, value, _options.Heading);
    }
}

/// <summary>
/// Options for <see cref="ColNoteEntryRegionParser"/>.
/// </summary>
public class ColNoteEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the role ID of the target note part (e.g.
    /// <c>evidence</c>).
    /// </summary>
    public string? PartRoleId { get; set; }

    /// <summary>
    /// Gets or sets the optional heading for the appended text.
    /// </summary>
    public string? Heading { get; set; }
}
