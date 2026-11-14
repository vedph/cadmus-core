using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColNoteEntryRegionParserTest
{
    private static ColNoteEntryRegionParser GetParser(string tag,
        string? roleId = null, string? heading = null)
    {
        ColNoteEntryRegionParser parser = new();
        parser.Configure(new ColNoteEntryRegionParserOptions
        {
            Tag = tag,
            PartRoleId = roleId,
            Heading = heading
        });
        return parser;
    }

    [Fact]
    public async Task Parse_Text_PreservesLineBreaks()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser("col-note"), context,
            "col-note", "  first  line\nsecond ");

        NotePart part = context.CurrentItem!.Parts.OfType<NotePart>().Single();
        Assert.Null(part.RoleId);
        Assert.Equal("first  line\nsecond", part.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n ")]
    public async Task Parse_Empty_NoPart(string? value)
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser("col-note"), context,
            "col-note", value);

        Assert.Empty(context.CurrentItem!.Parts);
    }

    [Fact]
    public async Task Parse_TwoColumnsWithHeadings_AppendsToSamePart()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser("col-a", "ico", "A"),
            context, "col-a", "alpha");
        await TestHelper.ParseColumnAsync(GetParser("col-b", "ico", "B"),
            context, "col-b", "beta");

        NotePart part = context.CurrentItem!.Parts.OfType<NotePart>().Single();
        Assert.Equal("ico", part.RoleId);
        Assert.Equal("### A\n\nalpha\n\n### B\n\nbeta", part.Text);
    }
}
