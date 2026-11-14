using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColPeriodsEntryRegionParserTest
{
    private static ColPeriodsEntryRegionParser GetParser(
        Action<ColPeriodsEntryRegionParserOptions>? setup = null)
    {
        ColPeriodsEntryRegionParserOptions options = new()
        {
            PeriodCount = 4,
            CertaintyTag = "col-certainty"
        };
        setup?.Invoke(options);
        ColPeriodsEntryRegionParser parser = new();
        parser.Configure(options);
        return parser;
    }

    private static async Task ParsePeriodsAsync(ColPeriodsEntryRegionParser parser,
        CadmusEntrySetContext context, string prefix, params string?[] values)
    {
        for (int i = 0; i < values.Length; i++)
        {
            await TestHelper.ParseColumnAsync(parser, context,
                prefix + (i + 1), values[i]);
        }
    }

    [Fact]
    public void RegionTags_PeriodsAndCertainty()
    {
        Assert.Equal(["col-period_1", "col-period_2", "col-period_3",
            "col-period_4", "col-certainty"], GetParser().RegionTags);
    }

    [Fact]
    public async Task Parse_Yes_AddsDateRanges()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColPeriodsEntryRegionParser parser = GetParser();

        await ParsePeriodsAsync(parser, context, "col-period_",
            "YES", "YES", null, "yes");

        AssertedHistoricalDatesPart part = context.CurrentItem!.Parts
            .OfType<AssertedHistoricalDatesPart>().Single();
        Assert.Equal(2, part.Dates.Count);

        Assert.Equal(-1000, part.Dates[0].A!.Value);
        Assert.True(part.Dates[0].A!.IsApproximate);
        Assert.Equal(-951, part.Dates[0].B!.Value);
        Assert.True(part.Dates[0].B!.IsApproximate);

        Assert.Equal(-925, part.Dates[1].A!.Value);
        Assert.Equal(-901, part.Dates[1].B!.Value);

        // bins are consumed
        Assert.False(context.Data.ContainsKey("periods"));
    }

    [Fact]
    public async Task Parse_NoYes_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await ParsePeriodsAsync(GetParser(), context, "col-period_",
            "NO", null, "no", null);

        Assert.Empty(context.CurrentItem!.Parts);
    }

    [Fact]
    public async Task Parse_Certainty_SetsRankOfAllDates()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColPeriodsEntryRegionParser parser = GetParser();

        await ParsePeriodsAsync(parser, context, "col-period_",
            "YES", null, "YES", null);
        await TestHelper.ParseColumnAsync(parser, context, "col-certainty",
            "Medium");

        AssertedHistoricalDatesPart part = context.CurrentItem!.Parts
            .OfType<AssertedHistoricalDatesPart>().Single();
        Assert.Equal(2, part.Dates.Count);
        Assert.All(part.Dates, d => Assert.Equal(2, d.Assertion!.Rank));
    }

    [Fact]
    public async Task Parse_CertaintyWithoutDates_Ignored()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-certainty",
            "high");

        Assert.Empty(context.CurrentItem!.Parts);
    }

    [Fact]
    public async Task Parse_CommentTarget_AddsChronologyBlock()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColPeriodsEntryRegionParser parser = GetParser(o =>
        {
            o.Target = PeriodsTarget.Comment;
            o.PartRoleId = "chr";
        });

        await ParsePeriodsAsync(parser, context, "col-period_",
            "YES", null, null, "YES");

        CommentPart part = context.CurrentItem!.Parts.OfType<CommentPart>()
            .Single();
        Assert.Equal("chr", part.RoleId);
        string[] lines = part.Text!.Split('\n');
        Assert.Equal("### Chronology", lines[0]);
        Assert.Equal("", lines[1]);
        Assert.Equal(2, lines.Skip(2).Count(l => l.StartsWith("- ")));
    }

    [Fact]
    public async Task Parse_CustomPrefixAndBins_Ok()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColPeriodsEntryRegionParser parser = GetParser(o =>
        {
            o.TagPrefix = "col-p";
            o.PeriodCount = 2;
            o.BaseYear = 1400;
            o.BinSize = 50;
        });
        Assert.Equal(["col-p1", "col-p2", "col-certainty"], parser.RegionTags);

        await ParsePeriodsAsync(parser, context, "col-p", null, "YES");

        AssertedHistoricalDatesPart part = context.CurrentItem!.Parts
            .OfType<AssertedHistoricalDatesPart>().Single();
        Assert.Equal(1450, part.Dates[0].A!.Value);
        Assert.Equal(1499, part.Dates[0].B!.Value);
    }
}
