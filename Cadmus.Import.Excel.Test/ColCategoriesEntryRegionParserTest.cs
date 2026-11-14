using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColCategoriesEntryRegionParserTest
{
    private const string TAG = "col-color";

    private static ColCategoriesEntryRegionParser GetParser(
        Action<ColCategoriesEntryRegionParserOptions>? setup = null)
    {
        ColCategoriesEntryRegionParserOptions options = new()
        {
            Tag = TAG,
            ThesaurusId = "colors@en",
            PartRoleId = "clr"
        };
        setup?.Invoke(options);
        ColCategoriesEntryRegionParser parser = new();
        parser.Configure(options);
        return parser;
    }

    private static CategoriesPart? GetPart(CadmusEntrySetContext context) =>
        context.CurrentItem!.Parts.OfType<CategoriesPart>().FirstOrDefault();

    [Fact]
    public async Task Parse_Label_AddsId()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, TAG, "Red");

        CategoriesPart part = GetPart(context)!;
        Assert.Equal("clr", part.RoleId);
        Assert.Equal(["r"], part.Categories);
    }

    [Fact]
    public async Task Parse_UnknownLabel_KeepsValue()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, TAG, "violet");

        Assert.Equal(["violet"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_SingleValueWithSeparator_NotSplit()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, TAG, "red;green");

        Assert.Equal(["red;green"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_MultipleLabels_AddsDistinctIds()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
            o.AllowMultipleValues = true), context, TAG, "red; green;red");

        Assert.Equal(["r", "g"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_NoThesaurusForLabel_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o => o.ThesaurusId = null),
            context, TAG, "red");

        Assert.Null(GetPart(context));
    }

    [Fact]
    public async Task Parse_Id_AddsId()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.ThesaurusId = "styles@en";
            o.ValueType = CategoryValueType.Id;
        }), context, TAG, "style.milanese");

        Assert.Equal(["style.milanese"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_IdWithPrefixAndLowercase_AddsPrefixedId()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.ThesaurusId = "styles@en";
            o.ValueType = CategoryValueType.Id;
            o.IdPrefix = "style.";
            o.LowercaseId = true;
        }), context, TAG, "Venetian");

        Assert.Equal(["style.venetian"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_UnknownIdWithoutThesaurus_KeepsId()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.ThesaurusId = null;
            o.ValueType = CategoryValueType.Id;
        }), context, TAG, "imp.good");

        Assert.Equal(["imp.good"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_Mappings_AddsMappedIds()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.ThesaurusId = "styles@en";
            o.AllowMultipleValues = true;
            o.Mappings = new Dictionary<string, string[]>
            {
                ["Demons and temptations"] = ["ico.demons", "ico.temptations"],
                ["Demons"] = ["ico.demons"]
            };
        }), context, TAG, "demons  AND temptations;DEMONS");

        Assert.Equal(["ico.demons", "ico.temptations"],
            GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_MappingsNotMatched_FallsBackToValueType()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(o =>
        {
            o.Mappings = new Dictionary<string, string[]>
            {
                ["x"] = ["b"]
            };
        }), context, TAG, "green");

        Assert.Equal(["g"], GetPart(context)!.Categories);
    }

    [Fact]
    public async Task Parse_TwoColumnsSameRole_SamePart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColCategoriesEntryRegionParser parser = GetParser(o =>
            o.ValueType = CategoryValueType.Id);

        await TestHelper.ParseColumnAsync(parser, context, TAG, "r");
        await TestHelper.ParseColumnAsync(parser, context, TAG, "g");

        Assert.Single(context.CurrentItem!.Parts);
        Assert.Equal(["r", "g"], GetPart(context)!.Categories);
    }
}
