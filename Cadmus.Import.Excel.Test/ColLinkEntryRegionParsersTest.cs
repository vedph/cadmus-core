using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColInternalLinkEntryRegionParserTest
{
    private static ColInternalLinkEntryRegionParser GetParser(
        string? setName = null)
    {
        ColInternalLinkEntryRegionParser parser = new();
        parser.Configure(new ColInternalLinkEntryRegionParserOptions
        {
            Tag = "col-site_id",
            Scope = "site",
            ItemIdsSet = setName
        });
        return parser;
    }

    [Fact]
    public async Task Parse_KnownEid_AddsInternalLink()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        TestHelper.AddItemIdsSet(context, "e1", "item-ids-s");

        await TestHelper.ParseColumnAsync(GetParser("item-ids-s"), context,
            "col-site_id", "e1");

        AssertedCompositeId link = context.CurrentItem!.Parts
            .OfType<PinLinksPart>().Single().Links.Single();
        Assert.Equal("site", link.Scope);
        Assert.Equal("item-1", link.Target!.ItemId);
        Assert.Equal("Pmd-1", link.Target.Gid);
    }

    [Fact]
    public async Task Parse_UnknownEid_NoLink()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        TestHelper.AddItemIdsSet(context);

        await TestHelper.ParseColumnAsync(GetParser(), context,
            "col-site_id", "e2");

        PinLinksPart? part = context.CurrentItem!.Parts.OfType<PinLinksPart>()
            .FirstOrDefault();
        Assert.True(part == null || part.Links.Count == 0);
    }

    [Fact]
    public async Task Parse_NoSet_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context,
            "col-site_id", "e1");

        Assert.Empty(context.CurrentItem!.Parts);
    }
}

public sealed class ColExternalLinkEntryRegionParserTest
{
    private static ColExternalLinkEntryRegionParser GetParser(
        char? separator = null, string? gidPrefix = null)
    {
        ColExternalLinkEntryRegionParser parser = new();
        parser.Configure(new ColExternalLinkEntryRegionParserOptions
        {
            Tag = "col-istc",
            Scope = "istc",
            LinkTag = "ed",
            Separator = separator,
            GidPrefix = gidPrefix
        });
        return parser;
    }

    [Fact]
    public async Task Parse_Single_AddsLink()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-istc",
            " ib00644000 ");

        AssertedCompositeId link = context.CurrentItem!.Parts
            .OfType<PinLinksPart>().Single().Links.Single();
        Assert.Equal("istc", link.Scope);
        Assert.Equal("ed", link.Tag);
        Assert.Equal("ib00644000", link.Target!.Gid);
        Assert.Equal("ib00644000", link.Target.Label);
        Assert.Null(link.Target.ItemId);
    }

    [Fact]
    public async Task Parse_Multiple_AddsLinkPerValue()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(';', "thema:"), context,
            "col-istc", "TE001676; TE002642");

        PinLinksPart part = context.CurrentItem!.Parts.OfType<PinLinksPart>()
            .Single();
        Assert.Equal(["thema:TE001676", "thema:TE002642"],
            part.Links.Select(l => l.Target!.Gid));
        Assert.Equal(["TE001676", "TE002642"],
            part.Links.Select(l => l.Target!.Label));
    }

    [Fact]
    public async Task Parse_Empty_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(';'), context,
            "col-istc", " ; ");

        Assert.Empty(context.CurrentItem!.Parts);
    }
}

public sealed class ColLinkAttributeEntryRegionParserTest
{
    private static ColLinkAttributeEntryRegionParser GetParser(
        LinkAttribute attribute)
    {
        ColLinkAttributeEntryRegionParser parser = new();
        parser.Configure(new ColLinkAttributeEntryRegionParserOptions
        {
            Tag = "col-attr",
            Scope = "gods",
            Attribute = attribute
        });
        return parser;
    }

    private static PinLinksPart AddLinks(CadmusEntrySetContext context)
    {
        PinLinksPart part = context.EnsurePartForCurrentItem<PinLinksPart>();
        part.Links.Add(new AssertedCompositeId
        {
            Scope = "gods",
            Target = new PinTarget { Gid = "a", Label = "a" }
        });
        part.Links.Add(new AssertedCompositeId
        {
            Scope = "other",
            Target = new PinTarget { Gid = "b", Label = "b" }
        });
        return part;
    }

    [Fact]
    public async Task Parse_Rank_SetsRankOfScopedLinks()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        PinLinksPart part = AddLinks(context);

        await TestHelper.ParseColumnAsync(GetParser(LinkAttribute.Rank),
            context, "col-attr", "low");

        Assert.Equal(3, part.Links[0].Assertion!.Rank);
        Assert.Null(part.Links[1].Assertion);
    }

    [Fact]
    public async Task Parse_Note_SetsNote()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        PinLinksPart part = AddLinks(context);

        await TestHelper.ParseColumnAsync(GetParser(LinkAttribute.Note),
            context, "col-attr", "a note");

        Assert.Equal("a note", part.Links[0].Note);
        Assert.Null(part.Links[1].Note);
    }

    [Fact]
    public async Task Parse_Tag_SetsTag()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        PinLinksPart part = AddLinks(context);

        await TestHelper.ParseColumnAsync(GetParser(LinkAttribute.Tag),
            context, "col-attr", "t");

        Assert.Equal("t", part.Links[0].Tag);
    }

    [Fact]
    public async Task Parse_NoLinks_NoChange()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(LinkAttribute.Note),
            context, "col-attr", "x");

        Assert.Empty(context.CurrentItem!.Parts);
    }
}
