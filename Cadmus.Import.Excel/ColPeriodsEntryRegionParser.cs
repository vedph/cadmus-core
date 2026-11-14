using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using Fusi.Antiquity.Chronology;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cadmus.Import.Excel;

/// <summary>
/// Base class for periods columns entry region parsers. This handles the
/// boolean columns <c>period_1</c> ... <c>period_N</c>, each corresponding to
/// a bin of years: the bins whose cell is <c>YES</c> are collected, and when
/// the last period column is reached, they are converted into one approximate
/// date range for each run of consecutive bins (see
/// <see cref="DateBinsConverter"/>), passed to <see cref="AddDates"/>.
/// <para>Optionally, this also handles a certainty column (following the
/// periods), whose value is passed to <see cref="ApplyCertainty"/>.</para>
/// <para>Derive from this class to target dates other than those handled
/// by <see cref="ColPeriodsEntryRegionParser"/>, eventually extending the
/// options.</para>
/// </summary>
/// <typeparam name="TOptions">The type of the options.</typeparam>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
public abstract class ColPeriodsEntryRegionParserBase<TOptions> :
    ColEntryRegionParser, IEntryRegionParser, IConfigurable<TOptions>
    where TOptions : ColPeriodsEntryRegionParserOptions, new()
{
    private const string PERIODS_KEY = "periods";

    /// <summary>
    /// Gets the options.
    /// </summary>
    protected TOptions Options { get; private set; } = new();

    /// <summary>
    /// Gets the tags of the regions that this parser can handle
    /// (<c>PREFIX1</c> ... <c>PREFIXN</c> plus the optional certainty tag,
    /// where <c>PREFIX</c> is <see cref="ColPeriodsEntryRegionParserOptions.
    /// TagPrefix"/>).
    /// </summary>
    public string[] RegionTags
    {
        get
        {
            List<string> tags = [.. Enumerable.Range(1, Options.PeriodCount)
                .Select(i => Options.TagPrefix +
                    i.ToString(CultureInfo.InvariantCulture))];
            if (Options.CertaintyTag != null) tags.Add(Options.CertaintyTag);
            return [.. tags];
        }
    }

    /// <summary>
    /// Gets a value indicating whether the value passed to
    /// <see cref="ParseValue" /> must be lowercased.
    /// </summary>
    protected override bool LowercaseValue => true;

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(TOptions options)
    {
        Options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
    }

    /// <summary>
    /// Builds the approximate dates corresponding to the specified bins.
    /// </summary>
    /// <param name="bins">The 1-based bin ordinals.</param>
    /// <returns>Dates.</returns>
    protected List<AssertedHistoricalDate> BuildDates(List<int> bins)
    {
        DateBinsConverter converter = new()
        {
            BaseYear = Options.BaseYear,
            BinSize = Options.BinSize
        };

        return [.. converter.Convert(bins).Select(r => new AssertedHistoricalDate
        {
            A = new Datation(r.Start) { IsApproximate = true },
            B = new Datation(r.End) { IsApproximate = true }
        })];
    }

    /// <summary>
    /// Adds the specified dates to their target.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="region">The region of the last period column.</param>
    /// <param name="dates">The dates to add.</param>
    protected abstract void AddDates(CadmusEntrySetContext context,
        EntryRegion region, List<AssertedHistoricalDate> dates);

    /// <summary>
    /// Applies the specified certainty value to the dates added for the
    /// current row. The default implementation sets the rank of all the dates
    /// in the current item's <see cref="AssertedHistoricalDatesPart"/> with
    /// role <see cref="ColPeriodsEntryRegionParserOptions.PartRoleId"/>.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="region">The certainty column region.</param>
    /// <param name="value">The certainty value (not null).</param>
    protected virtual void ApplyCertainty(CadmusEntrySetContext context,
        EntryRegion region, string value)
    {
        AssertedHistoricalDatesPart? part = context.CurrentItem!.Parts
            .OfType<AssertedHistoricalDatesPart>()
            .FirstOrDefault(p => Options.PartRoleId == null ||
                p.RoleId == Options.PartRoleId);

        if (part == null || part.Dates.Count == 0)
        {
            Logger?.LogWarning("Chronology certainty \"{Certainty}\" without " +
                "any date at region {Region}", value, region);
            return;
        }

        short rank = ImportHelper.LabelToRank(value, Logger);
        foreach (AssertedHistoricalDate date in part.Dates)
            ImportHelper.SetRank(date, rank);
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
        if (region.Tag == Options.CertaintyTag)
        {
            if (value != null) ApplyCertainty(context, region, value);
            return;
        }

        int n = int.Parse(region.Tag![Options.TagPrefix.Length..],
            CultureInfo.InvariantCulture);

        // cache YES period numbers until we get to the last one
        List<int> bins = context.GetData(PERIODS_KEY) as List<int> ?? [];
        if (ImportHelper.IsYes(value)) bins.Add(n);
        else if (value != null && value != "no")
        {
            Logger?.LogWarning("Unexpected value \"{Value}\" for period " +
                "at region {Region}", value, region);
        }

        if (n < Options.PeriodCount)
        {
            context.Data[PERIODS_KEY] = bins;
            return;
        }

        // last period: consume bins
        context.Data.Remove(PERIODS_KEY);
        if (bins.Count > 0) AddDates(context, region, BuildDates(bins));
    }
}

/// <summary>
/// Periods columns entry region parser. This converts the <c>YES</c> cells
/// of the periods columns into approximate date ranges (see
/// <see cref="ColPeriodsEntryRegionParserBase{TOptions}"/>), which are
/// added either to the item's <see cref="AssertedHistoricalDatesPart"/>, or to
/// a chronology block in the text of the item's <see cref="CommentPart"/>.
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-periods</c>.</para>
/// </summary>
[Tag("it.vedph.entry-region-parser.xlsx.col-periods")]
public sealed class ColPeriodsEntryRegionParser :
    ColPeriodsEntryRegionParserBase<ColPeriodsEntryRegionParserOptions>
{
    /// <summary>
    /// Adds the specified dates to their target.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="region">The region of the last period column.</param>
    /// <param name="dates">The dates to add.</param>
    protected override void AddDates(CadmusEntrySetContext context,
        EntryRegion region, List<AssertedHistoricalDate> dates)
    {
        switch (Options.Target)
        {
            case PeriodsTarget.Comment:
                StringBuilder sb = new();
                foreach (AssertedHistoricalDate date in dates)
                    sb.Append("- ").Append(date).Append('\n');

                CommentPart comment = context.EnsurePartForCurrentItem<CommentPart>(
                    Options.PartRoleId);
                comment.Text = ImportHelper.AppendToNote(comment.Text,
                    sb.ToString().TrimEnd(), Options.Heading);
                break;

            default:
                context.EnsurePartForCurrentItem<AssertedHistoricalDatesPart>(
                    Options.PartRoleId).Dates.AddRange(dates);
                break;
        }
    }
}

/// <summary>
/// Target of <see cref="ColPeriodsEntryRegionParser"/>.
/// </summary>
public enum PeriodsTarget
{
    /// <summary>
    /// Dates in an <see cref="AssertedHistoricalDatesPart"/>.
    /// </summary>
    Dates = 0,

    /// <summary>
    /// A chronology block in a <see cref="CommentPart"/>.
    /// </summary>
    Comment
}

/// <summary>
/// Options for <see cref="ColPeriodsEntryRegionParserBase{TOptions}"/>.
/// </summary>
public class ColPeriodsEntryRegionParserOptions : DisabledOptions
{
    /// <summary>
    /// Gets or sets the prefix of the period column tags, each followed by
    /// its 1-based ordinal. Default is <c>col-period_</c>.
    /// </summary>
    public string TagPrefix { get; set; } = "col-period_";

    /// <summary>
    /// Gets or sets the count of period columns. Default is 22.
    /// </summary>
    public int PeriodCount { get; set; } = 22;

    /// <summary>
    /// Gets or sets the base year of the first period. Default is -1000.
    /// </summary>
    public int BaseYear { get; set; } = -1000;

    /// <summary>
    /// Gets or sets the size of each period in years. Default is 25.
    /// </summary>
    public int BinSize { get; set; } = 25;

    /// <summary>
    /// Gets or sets the target. Default is dates.
    /// </summary>
    public PeriodsTarget Target { get; set; }

    /// <summary>
    /// Gets or sets the optional role ID of the target part.
    /// </summary>
    public string? PartRoleId { get; set; }

    /// <summary>
    /// Gets or sets the heading of the chronology block when
    /// <see cref="Target"/> is <see cref="PeriodsTarget.Comment"/>.
    /// </summary>
    public string? Heading { get; set; } = "Chronology";

    /// <summary>
    /// Gets or sets the optional tag of the certainty column, whose value
    /// is used as the rank of all the dates in the item's dates part.
    /// </summary>
    public string? CertaintyTag { get; set; }
}
