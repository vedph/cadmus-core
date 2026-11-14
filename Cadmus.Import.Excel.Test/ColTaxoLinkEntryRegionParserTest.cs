using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColTaxoLinkEntryRegionParserTest : IDisposable
{
    private readonly string _dir;

    public ColTaxoLinkEntryRegionParserTest()
    {
        _dir = Path.Combine(Path.GetTempPath(), "cadmus-taxo-" +
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        File.WriteAllText(Path.Combine(_dir, "trees.csv"),
            "id,name,note,flat\nplaces,Places,,0\ngods,Gods,\"a, note\",1\n");
        File.WriteAllText(Path.Combine(_dir, "nodes.csv"),
            "tree_n,parent_key,key,label\n" +
            "1,,rome,Rome\n" +
            "2,,zeus,Zeus\n" +
            "2,zeus,zeus_soter,\"Zeus, Soter\"\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    private ColTaxoLinkEntryRegionParser GetParser(char? separator = null,
        string? scope = null)
    {
        ColTaxoLinkEntryRegionParser parser = new();
        parser.Configure(new ColTaxoLinkEntryRegionParserOptions
        {
            Tag = "col-divinity",
            TreeId = "gods",
            TaxonomyDir = _dir,
            Separator = separator,
            Scope = scope
        });
        return parser;
    }

    [Fact]
    public async Task Parse_KnownLabel_AddsLinkWithNodeKey()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-divinity",
            "zeus, soter");

        var link = context.CurrentItem!.Parts.OfType<PinLinksPart>().Single()
            .Links.Single();
        Assert.Equal("gods", link.Scope);
        Assert.Equal("@TX:gods/zeus_soter", link.Target!.Gid);
        Assert.Equal("zeus, soter", link.Target.Label);
    }

    [Fact]
    public async Task Parse_Key_AddsLink()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(scope: "div"), context,
            "col-divinity", "zeus");

        var link = context.CurrentItem!.Parts.OfType<PinLinksPart>().Single()
            .Links.Single();
        Assert.Equal("div", link.Scope);
        Assert.Equal("@TX:gods/zeus", link.Target!.Gid);
    }

    [Fact]
    public async Task Parse_NodeOfOtherTree_Slugified()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(';'), context,
            "col-divinity", "Rome;Athèna Pàrthenos");

        PinLinksPart part = context.CurrentItem!.Parts.OfType<PinLinksPart>()
            .Single();
        Assert.Equal(["@TX:gods/rome", "@TX:gods/athena_parthenos"],
            part.Links.Select(l => l.Target!.Gid));
    }

    [Fact]
    public async Task Parse_NoTree_NoPart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColTaxoLinkEntryRegionParser parser = new();
        parser.Configure(new ColTaxoLinkEntryRegionParserOptions
        {
            Tag = "col-divinity"
        });

        await TestHelper.ParseColumnAsync(parser, context, "col-divinity", "zeus");

        Assert.Empty(context.CurrentItem!.Parts);
    }
}
