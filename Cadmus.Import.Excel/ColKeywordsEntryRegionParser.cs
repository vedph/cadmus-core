using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;

namespace Cadmus.Import.Excel;

/// <summary>
/// Column keywords entry region parser. The cell contains one or more
/// keywords, each added with the configured language and index ID to the
/// item's <see cref="IndexKeywordsPart"/>. Duplicate keywords are added
/// only once.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-keywords</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-keywords")]
public sealed class ColKeywordsEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColKeywordsEntryRegionParserOptions>
{
    private ColKeywordsEntryRegionParserOptions? _options;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColKeywordsEntryRegionParserOptions options)
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

        IList<string> values = _options!.Separator != null
            ? ImportHelper.GetValueList(value, _options.Lowercase,
                [_options.Separator.Value])
            : [_options.Lowercase ? value.ToLowerInvariant() : value];
        if (values.Count == 0) return;

        IndexKeywordsPart part = context
            .EnsurePartForCurrentItem<IndexKeywordsPart>(_options.PartRoleId);
        foreach (string v in values)
        {
            part.AddKeyword(new IndexKeyword
            {
                IndexId = _options.IndexId,
                Language = _options.Language,
                Value = v
            });
        }
    }
}

/// <summary>
/// Options for <see cref="ColKeywordsEntryRegionParser"/>.
/// </summary>
public class ColKeywordsEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the language of the keywords. Default is <c>en</c>.
    /// </summary>
    public string? Language { get; set; } = "en";

    /// <summary>
    /// Gets or sets the optional index ID of the keywords.
    /// </summary>
    public string? IndexId { get; set; }

    /// <summary>
    /// Gets or sets the optional separator for multiple values in the cell.
    /// Default is <c>;</c>. When null, the cell has a single value.
    /// </summary>
    public char? Separator { get; set; } = ';';

    /// <summary>
    /// Gets or sets a value indicating whether keywords must be lowercased.
    /// </summary>
    public bool Lowercase { get; set; }

    /// <summary>
    /// Gets or sets the optional role ID of the target keywords part.
    /// </summary>
    public string? PartRoleId { get; set; }
}
