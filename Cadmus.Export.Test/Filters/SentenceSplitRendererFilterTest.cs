using Cadmus.Export.Filters;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class SentenceSplitRendererFilterTest
{
    private static SentenceSplitTextFilter GetFilter(bool trimming = false)
    {
        SentenceSplitTextFilter filter = new();
        filter.Configure(new SentenceSplitRendererFilterOptions
        {
            EndMarkers = ".?!\u037e\u2026",
            NewLine = "\n",
            Trimming = trimming,
            CrLfRemoval = true,
            BlackOpeners = "(",
            BlackClosers = ")"
        });
        return filter;
    }

    [Fact]
    public async Task Apply_NoMarker_AppendedNL()
    {
        SentenceSplitTextFilter filter = GetFilter();

        string? result = (await filter.ApplyAsync("Hello, world"))?.ToString();

        Assert.Equal("Hello, world\n", result);
    }

    [Fact]
    public async Task Apply_InitialMarker_AppendedNL()
    {
        SentenceSplitTextFilter filter = GetFilter();

        string? result = (await filter.ApplyAsync("!Hello, world"))?.ToString();

        Assert.Equal("!Hello, world\n", result);
    }

    [Fact]
    public async Task Apply_MarkerSequence_AppendedNL()
    {
        SentenceSplitTextFilter filter = GetFilter(true);

        string? result = (await filter.ApplyAsync("Hello... world?!"))?.ToString();

        Assert.Equal("Hello...\nworld?!\n", result);
    }

    [Fact]
    public async Task Apply_Markers_Split()
    {
        SentenceSplitTextFilter filter = GetFilter();

        string? result = (await filter.ApplyAsync("Hello! I am world."))?.ToString();

        Assert.Equal("Hello! \nI am world.\n", result);
    }

    [Fact]
    public async Task Apply_MarkersWithBlack_Split()
    {
        SentenceSplitTextFilter filter = GetFilter(true);

        string? result = (await filter.ApplyAsync(
            "Hello (can you believe?) world! End."))?.ToString();

        Assert.Equal("Hello (can you believe?) world!\nEnd.\n", result);
    }

    [Fact]
    public async Task Apply_MarkersWithTrim_Split()
    {
        SentenceSplitTextFilter filter = GetFilter(true);

        string? result = (await filter.ApplyAsync(
            "Hello! I am world."))?.ToString();

        Assert.Equal("Hello!\nI am world.\n", result);
    }

    [Fact]
    public async Task Apply_MarkersWithCrLf_Split()
    {
        SentenceSplitTextFilter filter = GetFilter();

        string? result = (await filter.ApplyAsync(
            "Hello! I\r\nam world."))?.ToString();

        Assert.Equal("Hello! \nI am world.\n", result);
    }

    [Fact]
    public async Task Apply_MarkersWithCrLfTrim_Split()
    {
        SentenceSplitTextFilter filter = GetFilter(true);

        string? result = (await filter.ApplyAsync(
            "Hello! I\r\nam world."))?.ToString();

        Assert.Equal("Hello!\nI am world.\n", result);
    }
}
