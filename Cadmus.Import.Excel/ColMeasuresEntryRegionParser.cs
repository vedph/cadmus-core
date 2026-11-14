using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Mat.Bricks;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;

namespace Cadmus.Import.Excel;

/// <summary>
/// Column measures entry region parser. The cell contains a set of
/// dimensions separated by a separator, like <c>48x75</c> for height by width.
/// Each dimension becomes a measurement in the item's
/// <see cref="PhysicalMeasurementsPart"/> with the configured role, named
/// after the corresponding name in
/// <see cref="ColMeasuresEntryRegionParserOptions.Names"/> and with the
/// configured unit. Dimensions can use either dot or comma as decimal
/// separator. If the count of dimensions does not match the count of names,
/// or any dimension is not a number, an error is logged and nothing is added.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-measures</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-measures")]
public sealed class ColMeasuresEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColMeasuresEntryRegionParserOptions>
{
    private ColMeasuresEntryRegionParserOptions? _options;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColMeasuresEntryRegionParserOptions options)
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

        string[] names = _options.Names?.Length > 0
            ? _options.Names
            : ColMeasuresEntryRegionParserOptions.DefaultNames;
        char[] separators = _options.Separators?.Length > 0
            ? _options.Separators
            : ColMeasuresEntryRegionParserOptions.DefaultSeparators;

        string[] tokens = value.Split(separators,
            StringSplitOptions.RemoveEmptyEntries |
            StringSplitOptions.TrimEntries);
        if (tokens.Length != names.Length)
        {
            Logger?.LogError("Invalid measures \"{Value}\" at region {Region}: " +
                "expected {Count} values ({Names})", value, region,
                names.Length, string.Join(", ", names));
            return;
        }

        List<PhysicalMeasurement> measurements = [];
        for (int i = 0; i < tokens.Length; i++)
        {
            if (!ImportHelper.TryParseDecimal(tokens[i], out double n))
            {
                Logger?.LogError("Invalid {Name} \"{Token}\" in measures " +
                    "\"{Value}\" at region {Region}", names[i],
                    tokens[i], value, region);
                return;
            }
            measurements.Add(new PhysicalMeasurement
            {
                Name = names[i],
                Value = (float)n,
                Unit = _options.Unit
            });
        }

        context.EnsurePartForCurrentItem<PhysicalMeasurementsPart>(
            _options.PartRoleId).Measurements.AddRange(measurements);
    }
}

/// <summary>
/// Options for <see cref="ColMeasuresEntryRegionParser"/>.
/// </summary>
public class ColMeasuresEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// The default measurement names.
    /// </summary>
    public static readonly string[] DefaultNames = ["height", "width"];

    /// <summary>
    /// The default separators between dimensions.
    /// </summary>
    public static readonly char[] DefaultSeparators = ['x', 'X', '×'];

    /// <summary>
    /// Gets or sets the names of the measurements, in the order they appear
    /// in the cell. When null or empty, <see cref="DefaultNames"/> is used
    /// (<c>height</c>, <c>width</c>).
    /// </summary>
    /// <remarks>This has no default value, because configuration binding
    /// appends the configured items to the existing ones.</remarks>
    public string[]? Names { get; set; }

    /// <summary>
    /// Gets or sets the unit of all the measurements. Default is <c>mm</c>.
    /// </summary>
    public string? Unit { get; set; } = "mm";

    /// <summary>
    /// Gets or sets the separators between dimensions. When null or empty,
    /// <see cref="DefaultSeparators"/> is used (<c>x</c>, <c>X</c>, and
    /// <c>×</c>).
    /// </summary>
    /// <remarks>This has no default value, because configuration binding
    /// appends the configured items to the existing ones.</remarks>
    public char[]? Separators { get; set; }

    /// <summary>
    /// Gets or sets the optional role ID of the target measurements part.
    /// </summary>
    public string? PartRoleId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the column is required.
    /// </summary>
    public bool IsRequired { get; set; }
}
