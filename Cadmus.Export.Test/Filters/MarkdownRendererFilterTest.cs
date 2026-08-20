using Cadmus.Export.Filters;
using System.Threading.Tasks;
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
    public async Task Apply_NoRegion_Unchanged()
    {
        MarkdownTextFilter filter = GetFilter();

        string? result = (await filter.ApplyAsync("No markdown here"))?.ToString();

        Assert.Equal("No markdown here", result);
    }

    [Fact]
    public async Task Apply_Regions_Ok()
    {
        MarkdownTextFilter filter = GetFilter();

        string? result = (await filter.ApplyAsync(
            "Hello. <_md>This *is* MD.</_md> End."))?.ToString();

        Assert.Equal("Hello. <p>This <em>is</em> MD.</p>\n End.", result);
    }

    [Fact]
    public async Task Apply_WholeText_Ok()
    {
        MarkdownTextFilter filter = new();
        filter.Configure(new MarkdownRendererFilterOptions
        {
            Format = "html"
        });

        string? result = (await filter.ApplyAsync("This *is* MD."))?.ToString();

        Assert.Equal("<p>This <em>is</em> MD.</p>\n", result);
    }
}
