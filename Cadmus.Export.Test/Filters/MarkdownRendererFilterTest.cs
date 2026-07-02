using Cadmus.Export.Filters;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class MarkdownRendererFilterTest
{
    private static MarkdownTextFilter GetFilter()
    {
        MarkdownTextFilter filter = new();
        filter.Configure(new MarkdownRendererFilterOptions
        {
            Format = "html",
            MarkdownOpen = "<_md>",
            MarkdownClose = "</_md>"
        });
        return filter;
    }

    [Fact]
    public void Apply_NoRegion_Unchanged()
    {
        MarkdownTextFilter filter = GetFilter();

        string? result = filter.Apply("No markdown here")?.ToString();

        Assert.Equal("No markdown here", result);
    }

    [Fact]
    public void Apply_Regions_Ok()
    {
        MarkdownTextFilter filter = GetFilter();

        string? result = filter.Apply("Hello. <_md>This *is* MD.</_md> End.")?.ToString();

        Assert.Equal("Hello. <p>This <em>is</em> MD.</p>\n End.", result);
    }

    [Fact]
    public void Apply_WholeText_Ok()
    {
        MarkdownTextFilter filter = new();
        filter.Configure(new MarkdownRendererFilterOptions
        {
            Format = "html"
        });

        string? result = filter.Apply("This *is* MD.")?.ToString();

        Assert.Equal("<p>This <em>is</em> MD.</p>\n", result);
    }
}
