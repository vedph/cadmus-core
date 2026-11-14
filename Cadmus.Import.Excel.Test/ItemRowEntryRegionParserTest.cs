using Cadmus.Import.Proteus;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ItemRowEntryRegionParserTest
{
    private static ItemRowEntryRegionParser GetParser()
    {
        ItemRowEntryRegionParser parser = new();
        parser.Configure(new ItemRowEntryRegionParserOptions
        {
            FacetId = "woodblock"
        });
        return parser;
    }

    [Fact]
    public void RegionTags_Row()
    {
        Assert.Equal(["row"], GetParser().RegionTags);
    }

    [Fact]
    public async Task Parse_EnteredRow_AddsItemWithFacet()
    {
        ItemRowEntryRegionParser parser = GetParser();
        CadmusEntrySetContext context = TestHelper.CreateContext(false);
        context.Data["row-data"] = 1;
        context.Data["*global"] = 1;

        int next = await TestHelper.ParseRowAsync(parser, context);

        Assert.Equal(1, next);
        Assert.Single(context.Items);
        Assert.Equal("woodblock", context.CurrentItem!.FacetId);
        Assert.Equal("zeus", context.CurrentItem.CreatorId);
        // context was reset, preserving only global data
        Assert.False(context.Data.ContainsKey("row-data"));
        Assert.True(context.Data.ContainsKey("*global"));
    }

    [Fact]
    public async Task Parse_SameRowTwice_AddsItemOnce()
    {
        ItemRowEntryRegionParser parser = GetParser();
        CadmusEntrySetContext context = TestHelper.CreateContext(false);

        await TestHelper.ParseRowAsync(parser, context);
        var first = context.CurrentItem;
        int next = await TestHelper.ParseRowAsync(parser, context, enter: false);

        Assert.Equal(0, next);
        Assert.Single(context.Items);
        Assert.Same(first, context.CurrentItem);
    }

    [Fact]
    public async Task Parse_NewRow_ReplacesItem()
    {
        ItemRowEntryRegionParser parser = GetParser();
        CadmusEntrySetContext context = TestHelper.CreateContext(false);

        await TestHelper.ParseRowAsync(parser, context, 1);
        var first = context.CurrentItem;
        await TestHelper.ParseRowAsync(parser, context, 2);

        Assert.Single(context.Items);
        Assert.NotSame(first, context.CurrentItem);
    }
}
