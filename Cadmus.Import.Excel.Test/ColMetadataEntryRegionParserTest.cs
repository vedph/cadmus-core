using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColMetadataEntryRegionParserTest
{
    private const string TAG = "col-object_name";

    private static ColMetadataEntryRegionParser GetParser(
        Action<ColMetadataEntryRegionParserOptions>? setup = null)
    {
        ColMetadataEntryRegionParserOptions options = new()
        {
            Tag = TAG,
            Name = "eid"
        };
        setup?.Invoke(options);
        ColMetadataEntryRegionParser parser = new();
        parser.Configure(options);
        return parser;
    }

    [Fact]
    public void RegionTags_Configured()
    {
        Assert.Equal([TAG], GetParser().RegionTags);
    }

    [Fact]
    public async Task Parse_Value_AddsMetadatum()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        int next = await TestHelper.ParseColumnAsync(GetParser(), context, TAG,
            " ih001 ");

        Assert.Equal(3, next);
        MetadataPart part = context.CurrentItem!.Parts.OfType<MetadataPart>()
            .Single();
        Metadatum m = Assert.Single(part.Metadata);
        Assert.Equal("string", m.Type);
        Assert.Equal("eid", m.Name);
        Assert.Equal("ih001", m.Value);
        Assert.Equal("Item title", context.CurrentItem.Title);
    }

    [Fact]
    public async Task Parse_Type_AddsTypedMetadatum()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.Name = "cut-number";
            o.Type = "number";
        }), context, TAG, "5");

        Metadatum m = context.CurrentItem!.Parts.OfType<MetadataPart>()
            .Single().Metadata.Single();
        Assert.Equal("number", m.Type);
        Assert.Equal("cut-number", m.Name);
        Assert.Equal("5", m.Value);
    }

    [Fact]
    public async Task Parse_Empty_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, TAG, "  ");

        Assert.Empty(context.CurrentItem!.Parts);
    }

    [Fact]
    public async Task Parse_RequiredEmpty_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o => o.IsRequired = true),
            context, TAG, null);

        Assert.Empty(context.CurrentItem!.Parts);
    }

    [Fact]
    public async Task Parse_SetTitleOnly_SetsTitleWithoutPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.Name = null;
            o.SetTitle = true;
        }), context, TAG, "RGT_001");

        Assert.Equal("RGT_001", context.CurrentItem!.Title);
        Assert.Empty(context.CurrentItem.Parts);
    }

    [Fact]
    public async Task Parse_SetGroupId_SetsGroupId()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.Name = null;
            o.SetGroupId = true;
        }), context, TAG, "ih00232000");

        Assert.Equal("ih00232000", context.CurrentItem!.GroupId);
        Assert.Empty(context.CurrentItem.Parts);
    }

    [Fact]
    public async Task Parse_Separator_AddsMetadatumPerValue()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.Name = "inscription";
            o.Separator = ';';
        }), context, TAG, "S.ANTONIO; S.PAULO;");

        MetadataPart part = context.CurrentItem!.Parts.OfType<MetadataPart>()
            .Single();
        Assert.Equal(["S.ANTONIO", "S.PAULO"],
            part.Metadata.Select(m => m.Value));
        Assert.All(part.Metadata, m => Assert.Equal("inscription", m.Name));
    }

    [Fact]
    public async Task Parse_ReuseIds_Found_ReusesIds()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        TestHelper.AddItemIdsSet(context, "e1", "item-ids-c");

        await TestHelper.ParseColumnAsync(GetParser(o =>
            o.ReuseIdsFrom = "item-ids-c"), context, TAG, "e1");

        Assert.Equal("item-1", context.CurrentItem!.Id);
        MetadataPart part = context.CurrentItem.Parts.OfType<MetadataPart>()
            .Single();
        Assert.Equal("md-1", part.Id);
        Assert.Equal("item-1", part.ItemId);
    }

    [Fact]
    public async Task Parse_ReuseIds_NotFound_KeepsIds()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        string id = context.CurrentItem!.Id;
        TestHelper.AddItemIdsSet(context, "e1", "item-ids-c");

        await TestHelper.ParseColumnAsync(GetParser(o =>
            o.ReuseIdsFrom = "item-ids-c"), context, TAG, "e2");

        Assert.Equal(id, context.CurrentItem.Id);
    }

    [Fact]
    public async Task Parse_NoItem_Throws()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext(false);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            TestHelper.ParseColumnAsync(GetParser(), context, TAG, "x"));
    }

    [Fact]
    public async Task Parse_SkippedRow_Ignored()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ImportHelper.SkipRow(context);

        int next = await TestHelper.ParseColumnAsync(GetParser(), context, TAG,
            "x");

        Assert.Equal(3, next);
        Assert.Empty(context.Items);
    }
}
