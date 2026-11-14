using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;

namespace Cadmus.Import.Excel;

/// <summary>
/// Column categories entry region parser for any thesaurus. This maps
/// the cell's value(s) to thesaurus entry IDs and adds them to the
/// <see cref="CategoriesPart"/> with the configured role.
/// <para>Each value is resolved as follows:</para>
/// <list type="number">
/// <item><description>if <see cref="ColCategoriesEntryRegionParserOptions.
/// Mappings"/> has the value (matched case-insensitively after trimming and
/// collapsing whitespaces), its entry IDs are used;</description></item>
/// <item><description>else, when <see cref="ColCategoriesEntryRegionParserOptions.
/// ValueType"/> is <see cref="CategoryValueType.Label"/> (the default), the
/// value is an entry label, mapped to its ID via the thesaurus;
/// </description></item>
/// <item><description>else, the value is an entry ID, or its last segment,
/// optionally lowercased and prefixed with
/// <see cref="ColCategoriesEntryRegionParserOptions.IdPrefix"/>.
/// </description></item>
/// </list>
/// <para>Unless resolved from a label, when a thesaurus is configured the
/// resulting IDs are validated against it, logging an error for unknown
/// IDs (which are anyway preserved, so that no data is lost).</para>
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-categories</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-categories")]
public sealed class ColCategoriesEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColCategoriesEntryRegionParserOptions>
{
    private ColCategoriesEntryRegionParserOptions? _options;
    private Dictionary<string, string[]> _mappings =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColCategoriesEntryRegionParserOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);

        _mappings = new(StringComparer.OrdinalIgnoreCase);
        if (options.Mappings != null)
        {
            foreach (KeyValuePair<string, string[]> p in options.Mappings)
            {
                string? key = ImportHelper.FilterValue(p.Key, false);
                if (!string.IsNullOrEmpty(key)) _mappings[key] = p.Value ?? [];
            }
        }
    }

    private IList<string> ResolveIds(CadmusEntrySetContext context,
        EntryRegion region, string value)
    {
        // explicit mappings
        if (_mappings.TryGetValue(value, out string[]? mapped))
        {
            if (_options!.ThesaurusId != null)
            {
                foreach (string id in mapped)
                {
                    ImportHelper.ValidateThesaurusEntryId(context,
                        _options.ThesaurusId, id, Logger);
                }
            }
            return mapped;
        }

        // label
        if (_options!.ValueType == CategoryValueType.Label)
        {
            if (string.IsNullOrEmpty(_options.ThesaurusId))
            {
                Logger?.LogError("{Tag} column with no thesaurus configured " +
                    "at region {Region}", region.Tag, region);
                return [];
            }
            return [ImportHelper.GetThesaurusEntryId(
                context, region, _options.ThesaurusId, value, Logger)];
        }

        // ID
        string entryId = (_options.IdPrefix ?? "") +
            (_options.LowercaseId ? value.ToLowerInvariant() : value);
        if (_options.ThesaurusId != null)
        {
            ImportHelper.ValidateThesaurusEntryId(context, _options.ThesaurusId,
                entryId, Logger);
        }
        return [entryId];
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

        IList<string> values = _options.AllowMultipleValues
            ? ImportHelper.GetValueList(value, false, [_options.Separator])
            : [value];

        foreach (string v in values)
        {
            foreach (string id in ResolveIds(context, region, v))
            {
                CategoriesPart part = context
                    .EnsurePartForCurrentItem<CategoriesPart>(_options.PartRoleId);
                if (!part.Categories.Contains(id)) part.Categories.Add(id);
            }
        }
    }
}

/// <summary>
/// The type of the values found in the cells parsed by
/// <see cref="ColCategoriesEntryRegionParser"/>.
/// </summary>
public enum CategoryValueType
{
    /// <summary>
    /// Thesaurus entry labels, to be mapped to their IDs.
    /// </summary>
    Label = 0,

    /// <summary>
    /// Thesaurus entry IDs, or their last segment when a prefix is configured.
    /// </summary>
    Id
}

/// <summary>
/// Options for <see cref="ColCategoriesEntryRegionParser"/>.
/// </summary>
public class ColCategoriesEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the ID of the thesaurus to use, e.g.
    /// <c>categories_site-type@en</c>. This is required when
    /// <see cref="ValueType"/> is <see cref="CategoryValueType.Label"/>;
    /// otherwise, it is optional and used only to validate entry IDs.
    /// </summary>
    public string? ThesaurusId { get; set; }

    /// <summary>
    /// Gets or sets the type of the cell values. Default is
    /// <see cref="CategoryValueType.Label"/>.
    /// </summary>
    public CategoryValueType ValueType { get; set; }

    /// <summary>
    /// Gets or sets the optional prefix to prepend to each value when
    /// <see cref="ValueType"/> is <see cref="CategoryValueType.Id"/>, e.g.
    /// <c>style.</c> to get <c>style.venetian</c> from <c>venetian</c>.
    /// </summary>
    public string? IdPrefix { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to lowercase each value when
    /// <see cref="ValueType"/> is <see cref="CategoryValueType.Id"/>.
    /// </summary>
    public bool LowercaseId { get; set; }

    /// <summary>
    /// Gets or sets the optional explicit mappings, where each key is a cell
    /// value (matched case-insensitively after trimming and collapsing
    /// whitespaces) and each value is the list of the corresponding entry IDs.
    /// When a value is found here, its mapping takes precedence over
    /// <see cref="ValueType"/>.
    /// </summary>
    public Dictionary<string, string[]>? Mappings { get; set; }

    /// <summary>
    /// Gets or sets a value telling whether more than a single value is allowed
    /// in the parsed region. When true, the values are split by
    /// <see cref="Separator"/>.
    /// </summary>
    public bool AllowMultipleValues { get; set; }

    /// <summary>
    /// Gets or sets the separator character for multiple values.
    /// </summary>
    public char Separator { get; set; } = ';';

    /// <summary>
    /// Gets or sets the optional role ID of the target categories part.
    /// </summary>
    public string? PartRoleId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is required.
    /// </summary>
    public bool IsRequired { get; set; }
}
