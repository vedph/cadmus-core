using Xunit;

namespace Cadmus.Export.Test;

public sealed class MarkdownHelperTest
{
    [Fact]
    public void ConvertRegions_NoRegion_Nope()
    {
        const string text = "hello world";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", false);

        Assert.Equal(text, result);
    }

    [Fact]
    public void ConvertRegions_RegionAtStart_Ok()
    {
        const string text = "<_md>This **is** MD!</_md> plain";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", false);

        Assert.Equal("<p>This <strong>is</strong> MD!</p>\n plain", result);
    }

    [Fact]
    public void ConvertRegions_RegionAtEnd_Ok()
    {
        const string text = "plain <_md>This **is** MD!</_md>";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", false);

        Assert.Equal("plain <p>This <strong>is</strong> MD!</p>\n", result);
    }

    [Fact]
    public void ConvertRegions_RegionAtMid_Ok()
    {
        const string text = "alpha <_md>This **is** MD!</_md> beta";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", false);

        Assert.Equal("alpha <p>This <strong>is</strong> MD!</p>\n beta", result);
    }

    [Fact]
    public void ConvertRegions_Multiple_Ok()
    {
        const string text = "<_md>at *start*</_md> and " +
            "<_md>at *mid*</_md> and then " +
            "<_md>at *end*</_md>";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", false);

        Assert.Equal("<p>at <em>start</em></p>\n and " +
            "<p>at <em>mid</em></p>\n and then " +
            "<p>at <em>end</em></p>\n", result);
    }

    [Fact]
    public void ConvertRegions_MultiplePlain_Ok()
    {
        const string text = "<_md>at *start*</_md> and " +
            "<_md>at *mid*</_md> and then " +
            "<_md>at *end*</_md>";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", true);

        Assert.Equal("at start\n and " +
            "at mid\n and then " +
            "at end\n", result);
    }

    [Fact]
    public void ConvertRegions_RegionNotClosed_Ok()
    {
        const string text = "<_md>This **is** MD! plain";

        string result = MarkdownHelper.ConvertRegions(text,
            "<_md>", "</_md>", false);

        Assert.Equal("<p>This <strong>is</strong> MD! plain</p>\n", result);
    }
}
