using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ImportHelperTest
{
    private static readonly EntryRegion _region = new(0,
        new EntryRange(new EntryPoint(0), new EntryPoint(2)), "col-x");

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("  a   b ", "a b")]
    [InlineData("N/A", null)]
    [InlineData(" n/a ", null)]
    public void FilterValue_Ok(string? value, string? expected)
    {
        Assert.Equal(expected, ImportHelper.FilterValue(value, false));
    }

    [Fact]
    public void FilterValue_Lowercase_Ok()
    {
        Assert.Equal("ab c", ImportHelper.FilterValue(" AB  C ", true));
    }

    [Fact]
    public void GetValueList_DefaultSeparator_Ok()
    {
        Assert.Equal(["a", "b c"], ImportHelper.GetValueList("a, b  c,,", false));
    }

    [Fact]
    public void GetValueList_CustomSeparator_Ok()
    {
        Assert.Equal(["a", "b"], ImportHelper.GetValueList(" A ; B ", true, [';']));
    }

    [Fact]
    public void GetValueList_Empty_Empty()
    {
        Assert.Empty(ImportHelper.GetValueList(null, false));
    }

    [Theory]
    [InlineData("36.9507789", 36.9507789)]
    [InlineData("36,9507789", 36.9507789)]
    [InlineData(" -14.5 ", -14.5)]
    public void TryParseDecimal_Valid_Ok(string value, double expected)
    {
        Assert.True(ImportHelper.TryParseDecimal(value, out double d));
        Assert.Equal(expected, d, 7);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1.000,5")]
    public void TryParseDecimal_Invalid_False(string? value)
    {
        Assert.False(ImportHelper.TryParseDecimal(value, out _));
    }

    [Theory]
    [InlineData("YES", true)]
    [InlineData(" yes ", true)]
    [InlineData("NO", false)]
    [InlineData(null, false)]
    public void IsYes_Ok(string? value, bool expected)
    {
        Assert.Equal(expected, ImportHelper.IsYes(value));
    }

    [Fact]
    public void AppendToNote_Empty_NoSeparator()
    {
        Assert.Equal("### H\n\nx", ImportHelper.AppendToNote(null, "x", "H"));
    }

    [Fact]
    public void AppendToNote_NotEmpty_AppendsAfterBlankLine()
    {
        Assert.Equal("a\n\n### H\n\nx", ImportHelper.AppendToNote("a", "x", "H"));
        Assert.Equal("a\n\nx", ImportHelper.AppendToNote("a", "x"));
    }

    [Theory]
    [InlineData("high", 1)]
    [InlineData("Medium", 2)]
    [InlineData("low", 3)]
    [InlineData("other", 0)]
    [InlineData(null, 0)]
    public void LabelToRank_Ok(string? value, short expected)
    {
        Assert.Equal(expected, ImportHelper.LabelToRank(value));
    }

    [Fact]
    public void SetRank_NoAssertion_CreatesIt()
    {
        AssertedCompositeId link = new();
        ImportHelper.SetRank(link, 2);
        Assert.Equal(2, link.Assertion!.Rank);
    }

    [Theory]
    [InlineData("red", "r")]
    [InlineData("RED", "r")]
    [InlineData("light_blue", "lb")]
    [InlineData("unknown", "unknown")]
    public void GetThesaurusEntryId_Ok(string value, string expected)
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        Assert.Equal(expected, ImportHelper.GetThesaurusEntryId(
            context, _region, "colors@en", value, null));
    }

    [Fact]
    public void GetThesaurusEntryId_Alias_Ok()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        Assert.Equal("g", ImportHelper.GetThesaurusEntryId(
            context, _region, "my-colors@en", "green", null));
    }

    [Theory]
    [InlineData("colors@en", "r", true)]
    [InlineData("colors@en", "x", false)]
    [InlineData("unknown@en", "r", false)]
    public void ValidateThesaurusEntryId_Ok(string thesaurusId, string id,
        bool expected)
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        Assert.Equal(expected, ImportHelper.ValidateThesaurusEntryId(
            context, thesaurusId, id, null));
    }

    [Fact]
    public void SkipRow_RemovesItemAndFlagsRow()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        Assert.False(ImportHelper.IsRowSkipped(context));

        ImportHelper.SkipRow(context);

        Assert.Empty(context.Items);
        Assert.True(ImportHelper.IsRowSkipped(context));

        // the flag is not global, so it is removed on reset
        context.Reset();
        Assert.False(ImportHelper.IsRowSkipped(context));
    }

    [Fact]
    public void SetItemId_UpdatesParts()
    {
        Item item = new();
        item.Parts.Add(new NotePart { ItemId = item.Id });
        ImportHelper.SetItemId(item, "new-id");

        Assert.Equal("new-id", item.Id);
        Assert.Equal("new-id", item.Parts[0].ItemId);
    }

    [Fact]
    public void GetItemIdsSet_Missing_Throws()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        Assert.Throws<InvalidOperationException>(
            () => ImportHelper.GetItemIdsSet(context));
        Assert.Null(ImportHelper.TryGetItemIdsSet(context));
    }

    [Fact]
    public void GetItemIdsSet_Named_Ok()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ExportedItemIdSet set = TestHelper.AddItemIdsSet(context, "e1",
            "item-ids-s");

        Assert.Same(set, ImportHelper.GetItemIdsSet(context, "item-ids-s"));
        Assert.Null(ImportHelper.TryGetItemIdsSet(context));
    }

    [Fact]
    public void FindItemId_Ok()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ExportedItemIdSet set = TestHelper.AddItemIdsSet(context);

        Assert.Equal("item-1", ImportHelper.FindItemId(set, "e1")?.Id);
        Assert.Null(ImportHelper.FindItemId(set, "e2"));
    }

    [Fact]
    public void AddInternalLink_Found_AddsLink()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ExportedItemIdSet set = TestHelper.AddItemIdsSet(context);
        PinLinksPart part = new();

        AssertedCompositeId? link = ImportHelper.AddInternalLink(part, "e1", set,
            null, "site");

        Assert.NotNull(link);
        Assert.Single(part.Links);
        Assert.Equal("site", link.Scope);
        Assert.Equal("Pmd-1", link.Target!.Gid);
        Assert.Equal("e1|item-1 (metadata)", link.Target.Label);
        Assert.Equal("item-1", link.Target.ItemId);
        Assert.Equal("md-1", link.Target.PartId);
        Assert.Equal("it.vedph.metadata", link.Target.PartTypeId);
        Assert.Equal("eid", link.Target.Name);
        Assert.Equal("e1", link.Target.Value);
    }

    [Fact]
    public void AddInternalLink_NotFound_Null()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ExportedItemIdSet set = TestHelper.AddItemIdsSet(context);
        PinLinksPart part = new();

        Assert.Null(ImportHelper.AddInternalLink(part, "e2", set));
        Assert.Empty(part.Links);
    }

    [Fact]
    public void GetDateValue_Ok()
    {
        Assert.Equal(new DateOnly(2024, 3, 5), ImportHelper.GetDateValue("5/3/2024"));
        Assert.Null(ImportHelper.GetDateValue("x"));
    }

    [Fact]
    public void GetIntValue_Ok()
    {
        Assert.Equal(12, ImportHelper.GetIntValue(" 12 "));
        Assert.Equal(0, ImportHelper.GetIntValue("x"));
    }

    [Theory]
    [InlineData("si", true)]
    [InlineData("NO", false)]
    [InlineData("x", null)]
    [InlineData(null, null)]
    public void GetNullableBooleanValue_Ok(string? value, bool? expected)
    {
        Assert.Equal(expected, ImportHelper.GetNullableBooleanValue(value));
    }

    [Fact]
    public void GetItemIdsKey_Ok()
    {
        Assert.Equal(ImportHelper.ITEM_IDS_KEY, ImportHelper.GetItemIdsKey(null));
        Assert.Equal(ImportHelper.ITEM_IDS_KEY + ":a", ImportHelper.GetItemIdsKey("a"));
        List<string> keys = [ImportHelper.GetItemIdsKey("a")];
        Assert.StartsWith("*", keys[0]);
    }
}
