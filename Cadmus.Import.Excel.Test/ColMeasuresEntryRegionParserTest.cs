using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ColMeasuresEntryRegionParserTest
{
    private static ColMeasuresEntryRegionParser GetParser(
        ColMeasuresEntryRegionParserOptions? options = null)
    {
        ColMeasuresEntryRegionParser parser = new();
        parser.Configure(options ?? new ColMeasuresEntryRegionParserOptions
        {
            Tag = "col-size",
            PartRoleId = "wblk"
        });
        return parser;
    }

    [Theory]
    [InlineData("48x75")]
    [InlineData(" 48 X 75 ")]
    [InlineData("48×75")]
    public async Task Parse_HxW_AddsHeightAndWidth(string value)
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-size", value);

        PhysicalMeasurementsPart part = context.CurrentItem!.Parts
            .OfType<PhysicalMeasurementsPart>().Single();
        Assert.Equal("wblk", part.RoleId);
        Assert.Equal(2, part.Measurements.Count);
        Assert.Equal("height", part.Measurements[0].Name);
        Assert.Equal(48, part.Measurements[0].Value);
        Assert.Equal("mm", part.Measurements[0].Unit);
        Assert.Equal("width", part.Measurements[1].Name);
        Assert.Equal(75, part.Measurements[1].Value);
        Assert.Equal("mm", part.Measurements[1].Unit);
    }

    [Fact]
    public async Task Parse_CustomNamesAndDecimals_Ok()
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(
            new ColMeasuresEntryRegionParserOptions
            {
                Tag = "col-size",
                Names = ["height", "width", "depth"],
                Unit = "cm"
            }), context, "col-size", "1,5x2.25x3");

        PhysicalMeasurementsPart part = context.CurrentItem!.Parts
            .OfType<PhysicalMeasurementsPart>().Single();
        Assert.Equal([1.5f, 2.25f, 3f], part.Measurements.Select(m => m.Value));
        Assert.All(part.Measurements, m => Assert.Equal("cm", m.Unit));
    }

    [Theory]
    [InlineData("48")]
    [InlineData("48x75x3")]
    [InlineData("48xabc")]
    public async Task Parse_Invalid_NoPart(string value)
    {
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(GetParser(), context, "col-size", value);

        Assert.Empty(context.CurrentItem!.Parts);
    }
}
