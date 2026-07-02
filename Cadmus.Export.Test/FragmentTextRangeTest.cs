using System.Collections.Generic;
using Xunit;

namespace Cadmus.Export.Test;

public sealed class FragmentTextRangeTest
{
    private static List<AnnotatedTextRange> GetRangesWithSingleFr()
    {
        // 012345678901234567
        // que bixit annos XX
        // ..O............... fr1
        // ....O............. fr2
        // ..PPP............. fr3
        // ....CCCCCCCCCCC... fr4
        // 012345678901234567
        return
        [
            new AnnotatedTextRange(2, 2, "fr1"),
            new AnnotatedTextRange(4, 4, "fr2"),
            new AnnotatedTextRange(2, 4, "fr3"),
            new AnnotatedTextRange(4, 14, "fr4")
        ];
    }

    [Fact]
    public void MergeRanges_SingleFragments_Ok()
    {
        IList<AnnotatedTextRange> ranges = AnnotatedTextRange.GetConsecutiveRanges(
            0, 17, GetRangesWithSingleFr());
        Assert.Equal(6, ranges.Count);

        // 0-1 ("qu") = no fragments
        Assert.Equal(0, ranges[0].Start);
        Assert.Equal(1, ranges[0].End);
        Assert.Empty(ranges[0].FragmentIds);

        // 2-2 ("e") = fr1, fr3
        Assert.Equal(2, ranges[1].Start);
        Assert.Equal(2, ranges[1].End);
        Assert.Equal(2, ranges[1].FragmentIds.Count);
        Assert.Equal("fr1", ranges[1].FragmentIds[0]);
        Assert.Equal("fr3", ranges[1].FragmentIds[1]);

        // 3-3 (" ") = fr3
        Assert.Equal(3, ranges[2].Start);
        Assert.Equal(3, ranges[2].End);
        Assert.Single(ranges[2].FragmentIds);
        Assert.Equal("fr3", ranges[2].FragmentIds[0]);

        // 4-4 ("b") = fr2, fr3, fr4
        Assert.Equal(4, ranges[3].Start);
        Assert.Equal(4, ranges[3].End);
        Assert.Equal(3, ranges[3].FragmentIds.Count);
        Assert.Equal("fr2", ranges[3].FragmentIds[0]);
        Assert.Equal("fr3", ranges[3].FragmentIds[1]);
        Assert.Equal("fr4", ranges[3].FragmentIds[2]);

        // 5-14 ("ixit annos") = fr4
        Assert.Equal(5, ranges[4].Start);
        Assert.Equal(14, ranges[4].End);
        Assert.Single(ranges[4].FragmentIds);
        Assert.Equal("fr4", ranges[4].FragmentIds[0]);

        // 15-17 (" XX") = no fragments
        Assert.Equal(15, ranges[5].Start);
        Assert.Equal(17, ranges[5].End);
        Assert.Empty(ranges[5].FragmentIds);
    }
}
