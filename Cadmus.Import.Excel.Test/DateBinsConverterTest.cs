using System;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class DateBinsConverterTest
{
    [Fact]
    public void Convert_EmptySelection_ReturnsEmptyList()
    {
        DateBinsConverter converter = new();

        Assert.Empty(converter.Convert([]));
    }

    [Fact]
    public void Convert_NullSelection_ThrowsArgumentNullException()
    {
        DateBinsConverter converter = new();

        Assert.Throws<ArgumentNullException>(() => converter.Convert(null!));
    }

    [Fact]
    public void Convert_SingleBin_ReturnsItsYearRange()
    {
        DateBinsConverter converter = new();

        Assert.Equal(
            [(Start: -1000, End: -976)],
            converter.Convert([1]));
    }

    [Fact]
    public void Convert_UnsortedDuplicateAndAdjacentBins_MergesIntoOneRange()
    {
        DateBinsConverter converter = new();

        Assert.Equal(
            [(Start: -1000, End: -901)],
            converter.Convert([4, 2, 3, 2, 1]));
    }

    [Fact]
    public void Convert_NonAdjacentBins_ReturnsSeparateRanges()
    {
        DateBinsConverter converter = new();

        Assert.Equal(
            [
                (Start: -1000, End: -976),
                (Start: -950, End: -901),
                (Start: -850, End: -826)
            ],
            converter.Convert([7, 1, 4, 3, 7]));
    }

    [Fact]
    public void Convert_CustomBaseYearAndBinSize_UsesConfiguredValues()
    {
        DateBinsConverter converter = new()
        {
            BaseYear = 1000,
            BinSize = 10
        };

        Assert.Equal(
            [(Start: 1010, End: 1029)],
            converter.Convert([2, 3]));
    }
}
