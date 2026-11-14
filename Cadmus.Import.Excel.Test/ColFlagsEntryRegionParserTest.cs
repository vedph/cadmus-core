using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColFlagsEntryRegionParserTest
{
    private static ColFlagsEntryRegionParser GetParser()
    {
        ColFlagsEntryRegionParser parser = new();
        parser.Configure(new ColFlagsEntryRegionParserOptions
        {
            Flags = new Dictionary<string, string>
            {
                ["col-red"] = "r",
                ["col-green"] = "g"
            },
            ThesaurusId = "colors@en",
            PartRoleId = "feats"
        });
        return parser;
    }

    [Fact]
    public void RegionTags_FlagKeys()
    {
        Assert.Equal(["col-red", "col-green"], GetParser().RegionTags);
    }

    [Fact]
    public async Task Parse_Yes_AddsId()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();
        ColFlagsEntryRegionParser parser = GetParser();

        await TestHelper.ParseColumnAsync(parser, context, "col-red", "YES");
        await TestHelper.ParseColumnAsync(parser, context, "col-green", "yes");
        await TestHelper.ParseColumnAsync(parser, context, "col-red", "YES");

        CategoriesPart part = context.CurrentItem!.Parts
            .OfType<CategoriesPart>().Single();
        Assert.Equal("feats", part.RoleId);
        Assert.Equal(["r", "g"], part.Categories);
    }

    [Theory]
    [InlineData("NO")]
    [InlineData(null)]
    [InlineData("maybe")]
    public async Task Parse_NotYes_Ignored(string? value)
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-red", value);

        Assert.Empty(context.CurrentItem!.Parts);
    }
}
