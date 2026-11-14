using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColKeywordsEntryRegionParserTest
{
    private static ColKeywordsEntryRegionParser GetParser(
        ColKeywordsEntryRegionParserOptions? options = null)
    {
        ColKeywordsEntryRegionParser parser = new();
        parser.Configure(options ?? new ColKeywordsEntryRegionParserOptions
        {
            Tag = "col-kw"
        });
        return parser;
    }

    [Fact]
    public async Task Parse_Multiple_AddsDistinctKeywords()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-kw",
            "castity; assault;capture;castity");

        IndexKeywordsPart part = context.CurrentItem!.Parts
            .OfType<IndexKeywordsPart>().Single();
        // a duplicate replaces the previous keyword, so it gets appended
        Assert.Equal(["assault", "capture", "castity"],
            part.Keywords.Select(k => k.Value));
        Assert.All(part.Keywords, k =>
        {
            Assert.Equal("en", k.Language);
            Assert.Null(k.IndexId);
        });
    }

    [Fact]
    public async Task Parse_SingleLowercaseWithIndex_Ok()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(
            new ColKeywordsEntryRegionParserOptions
            {
                Tag = "col-kw",
                Separator = null,
                Lowercase = true,
                Language = "ita",
                IndexId = "x",
                PartRoleId = "r"
            }), context, "col-kw", "Alpha; Beta");

        IndexKeywordsPart part = context.CurrentItem!.Parts
            .OfType<IndexKeywordsPart>().Single();
        Assert.Equal("r", part.RoleId);
        IndexKeyword k = Assert.Single(part.Keywords);
        Assert.Equal("alpha; beta", k.Value);
        Assert.Equal("ita", k.Language);
        Assert.Equal("x", k.IndexId);
    }

    [Fact]
    public async Task Parse_Empty_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-kw", ";;");

        Assert.Empty(context.CurrentItem!.Parts);
    }
}
