using System;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Export.Test;

public sealed class AnnotatedTextRangeTest
{
    [Fact]
    public void GetConsecutiveRanges_NoRanges_ReturnsSimpleRange()
    {
        const int start = 0;
        const int end = 10;
        IList<AnnotatedTextRange> ranges = [];

        IList<AnnotatedTextRange> result = AnnotatedTextRange.GetConsecutiveRanges(
            start, end, ranges);

        Assert.Single(result);
        Assert.Equal(start, result[0].Start);
        Assert.Equal(end, result[0].End);
        Assert.Empty(result[0].FragmentIds);
    }

    [Fact]
    public void GetConsecutiveRanges_InvalidStartEnd_ThrowsArgumentException()
    {
        const int start = 10;
        const int end = 5;
        IList<AnnotatedTextRange> ranges = [];

        Assert.Throws<ArgumentException>(() =>
            AnnotatedTextRange.GetConsecutiveRanges(start, end, ranges));
    }

    [Fact]
    public void GetConsecutiveRanges_NullRanges_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            AnnotatedTextRange.GetConsecutiveRanges(0, 10, null!));
    }

    [Fact]
    public void GetConsecutiveRanges_SingleRange_ReturnsOriginalRange()
    {
        const int start = 0;
        const int end = 10;
        List<AnnotatedTextRange> ranges =
        [
            new(start, end, "fr1")
        ];

        IList<AnnotatedTextRange> result = AnnotatedTextRange.GetConsecutiveRanges(
            start, end, ranges);

        Assert.Single(result);
        Assert.Equal(start, result[0].Start);
        Assert.Equal(end, result[0].End);
        Assert.Single(result[0].FragmentIds);
        Assert.Equal("fr1", result[0].FragmentIds[0]);
    }

    [Fact]
    public void GetConsecutiveRanges_RangesWithGaps_FillsGaps()
    {
        const int start = 0;
        const int end = 10;
        List<AnnotatedTextRange> ranges =
        [
            new(0, 3, "fr1"),
            new(7, 10, "fr2")
        ];

        IList<AnnotatedTextRange> result = AnnotatedTextRange.GetConsecutiveRanges(
            start, end, ranges);

        Assert.Equal(3, result.Count);

        // first range with fr1
        Assert.Equal(0, result[0].Start);
        Assert.Equal(3, result[0].End);
        Assert.Contains("fr1", result[0].FragmentIds);

        // gap in the middle with no fragments
        Assert.Equal(4, result[1].Start);
        Assert.Equal(6, result[1].End);
        Assert.Empty(result[1].FragmentIds);

        // last range with fr2
        Assert.Equal(7, result[2].Start);
        Assert.Equal(10, result[2].End);
        Assert.Contains("fr2", result[2].FragmentIds);
    }

    [Fact]
    public void GetConsecutiveRanges_RangesOutsideRequestedSpan_IgnoresOutsideRanges()
    {
        const int start = 5;
        const int end = 15;
        List<AnnotatedTextRange> ranges =
        [
            new(0, 4, "fr1"),
            new(8, 12, "fr2"),
            new(16, 20, "fr3")
        ];

        IList<AnnotatedTextRange> result = AnnotatedTextRange.GetConsecutiveRanges(
            start, end, ranges);

        Assert.Equal(3, result.Count);

        // first segment (gap with no fragments)
        Assert.Equal(5, result[0].Start);
        Assert.Equal(7, result[0].End);
        Assert.Empty(result[0].FragmentIds);

        // middle segment with fr2
        Assert.Equal(8, result[1].Start);
        Assert.Equal(12, result[1].End);
        Assert.Single(result[1].FragmentIds);
        Assert.Equal("fr2", result[1].FragmentIds[0]);

        // last segment (gap with no fragments)
        Assert.Equal(13, result[2].Start);
        Assert.Equal(15, result[2].End);
        Assert.Empty(result[2].FragmentIds);
    }

    [Fact]
    public void GetConsecutiveRanges_PartiallyOutsideRanges_AdjustsRangeBoundaries()
    {
        const int start = 5;
        const int end = 15;
        List<AnnotatedTextRange> ranges =
        [
            new(2, 7, "fr1"),
            new(8, 12, "fr2"),
            new(13, 18, "fr3")
        ];

        IList<AnnotatedTextRange> result = AnnotatedTextRange.GetConsecutiveRanges(
            start, end, ranges);

        Assert.Equal(3, result.Count);

        // first segment with fr1 (adjusted)
        Assert.Equal(5, result[0].Start);
        Assert.Equal(7, result[0].End);
        Assert.Single(result[0].FragmentIds);
        Assert.Equal("fr1", result[0].FragmentIds[0]);

        // middle segment with fr2
        Assert.Equal(8, result[1].Start);
        Assert.Equal(12, result[1].End);
        Assert.Single(result[1].FragmentIds);
        Assert.Equal("fr2", result[1].FragmentIds[0]);

        // last segment with fr3 (adjusted)
        Assert.Equal(13, result[2].Start);
        Assert.Equal(15, result[2].End);
        Assert.Single(result[2].FragmentIds);
        Assert.Equal("fr3", result[2].FragmentIds[0]);
    }

    [Fact]
    public void GetConsecutiveRanges_MultipleFragmentIds_SortsFragmentIds()
    {
        const int start = 0;
        const int end = 10;
        List<AnnotatedTextRange> ranges =
        [
            new(0, 5, "fr2", "fr1"),
            new(3, 8, "fr3"),
            new(6, 10, "fr5", "fr4")
        ];

        IList<AnnotatedTextRange> result = AnnotatedTextRange.GetConsecutiveRanges(
            start, end, ranges);

        // there should be 4 ranges with different fragment ID combinations
        Assert.Equal(4, result.Count);

        // first segment (0-2) with fr1, fr2
        Assert.Equal(0, result[0].Start);
        Assert.Equal(2, result[0].End);
        Assert.Equal(2, result[0].FragmentIds.Count);
        Assert.Equal("fr1", result[0].FragmentIds[0]);
        Assert.Equal("fr2", result[0].FragmentIds[1]);

        // second segment (3-5) with fr1, fr2, fr3
        Assert.Equal(3, result[1].Start);
        Assert.Equal(5, result[1].End);
        Assert.Equal(3, result[1].FragmentIds.Count);
        Assert.Equal("fr1", result[1].FragmentIds[0]);
        Assert.Equal("fr2", result[1].FragmentIds[1]);
        Assert.Equal("fr3", result[1].FragmentIds[2]);

        // third segment (6-8) with fr3, fr4, fr5
        Assert.Equal(6, result[2].Start);
        Assert.Equal(8, result[2].End);
        Assert.Equal(3, result[2].FragmentIds.Count);
        Assert.Equal("fr3", result[2].FragmentIds[0]);
        Assert.Equal("fr4", result[2].FragmentIds[1]);
        Assert.Equal("fr5", result[2].FragmentIds[2]);

        // fourth segment (9-10) with fr4, fr5
        Assert.Equal(9, result[3].Start);
        Assert.Equal(10, result[3].End);
        Assert.Equal(2, result[3].FragmentIds.Count);
        Assert.Equal("fr4", result[3].FragmentIds[0]);
        Assert.Equal("fr5", result[3].FragmentIds[1]);
    }
}
