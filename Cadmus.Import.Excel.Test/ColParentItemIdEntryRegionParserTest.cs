using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColParentItemIdEntryRegionParserTest
{
    private sealed class HookedParser : ColParentItemIdEntryRegionParser
    {
        public IItem? Resolved { get; private set; }

        protected override void OnParentItemResolved(
            CadmusEntrySetContext context, IItem item)
        {
            Resolved = item;
        }
    }

    private static T GetParser<T>() where T : ColParentItemIdEntryRegionParser, new()
    {
        T parser = new();
        parser.Configure(new ColParentItemIdEntryRegionParserOptions
        {
            Tag = "col-site_id",
            ItemIdsSet = "item-ids-s"
        });
        return parser;
    }

    [Fact]
    public async Task Parse_KnownEid_SetsParentIdAndTitle()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        context.EnsurePartForCurrentItem<NotePart>();
        TestHelper.AddItemIdsSet(context, "s1", "item-ids-s");
        HookedParser parser = GetParser<HookedParser>();

        await TestHelper.ParseColumnAsync(parser, context, "col-site_id", "s1");

        IItem item = context.CurrentItem!;
        Assert.Equal("item-1", item.Id);
        Assert.Equal("Parent", item.Title);
        Assert.Equal("item-1", item.Parts[0].ItemId);
        Assert.Same(item, parser.Resolved);
    }

    [Fact]
    public async Task Parse_UnknownEid_SkipsRow()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        TestHelper.AddItemIdsSet(context, "s1", "item-ids-s");
        HookedParser parser = GetParser<HookedParser>();

        await TestHelper.ParseColumnAsync(parser, context, "col-site_id", "s2");

        Assert.Empty(context.Items);
        Assert.True(ImportHelper.IsRowSkipped(context));
        Assert.Null(parser.Resolved);
    }

    [Fact]
    public async Task Parse_Empty_SkipsRow()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        TestHelper.AddItemIdsSet(context, "s1", "item-ids-s");

        await TestHelper.ParseColumnAsync(
            GetParser<ColParentItemIdEntryRegionParser>(), context,
            "col-site_id", null);

        Assert.True(ImportHelper.IsRowSkipped(context));
    }

    [Fact]
    public async Task Parse_SkippedRow_NextColumnsIgnored()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        TestHelper.AddItemIdsSet(context, "s1", "item-ids-s");
        await TestHelper.ParseColumnAsync(
            GetParser<ColParentItemIdEntryRegionParser>(), context,
            "col-site_id", "s2");

        ColNoteEntryRegionParser note = new();
        note.Configure(new ColNoteEntryRegionParserOptions { Tag = "col-note" });
        int next = await TestHelper.ParseColumnAsync(note, context, "col-note",
            "x");

        Assert.Equal(3, next);
        Assert.Empty(context.Items);
    }
}
