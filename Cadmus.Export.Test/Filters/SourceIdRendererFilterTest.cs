using Cadmus.Export.Filters;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class SourceIdRendererFilterTest
{
    private static CadmusRendererContext GetContext()
    {
        CadmusRendererContext context = new();
        context.MapSourceId("seg",
            "db66b931-d468-4478-a6ae-d9e56e9431b9/0");
        context.MapSourceId("seg",
            "db66b931-d468-4478-a6ae-d9e56e9431b9/1");
        return context;
    }

    [Fact]
    public void Apply_NoTags_Unchanged()
    {
        SourceIdTextFilter filter = new();

        string? result = filter.Apply("hello world", GetContext())?.ToString();

        Assert.NotNull(result);
        Assert.Equal("hello world", result);
    }

    [Fact]
    public void Apply_TagsWithoutMatch_Unresolved()
    {
        SourceIdTextFilter filter = new();

        string? result = filter.Apply("hello #[unknown]# world",
            GetContext())?.ToString();

        Assert.NotNull(result);
        Assert.Equal("hello unknown world", result);
    }

    [Fact]
    public void Apply_TagsWithoutMatchWithOmit_Omitted()
    {
        SourceIdTextFilter filter = new();
        filter.Configure(new SourceIdRendererFilterOptions
        {
            OmitUnresolved = true
        });

        string? result = filter.Apply("hello #[unknown]# world",
            GetContext())?.ToString();

        Assert.NotNull(result);
        Assert.Equal("hello  world", result);
    }

    [Fact]
    public void Apply_TagsWithMatch_Ok()
    {
        SourceIdTextFilter filter = new();

        string? result = filter.Apply(
            "hello #[seg/db66b931-d468-4478-a6ae-d9e56e9431b9/0]# world",
            GetContext())?.ToString();

        Assert.NotNull(result);
        Assert.Equal("hello seg1 world", result);
    }
}
