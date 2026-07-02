using Cadmus.Export.Filters;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class Iso639FilterTest
{
    [Fact]
    public void Apply_NoMatch_Unchanged()
    {
        Iso639TextFilter filter = new();
        const string text = "Hello, world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal(text, filtered);
    }

    [Fact]
    public void Apply_MatchInvalidCode_Code()
    {
        Iso639TextFilter filter = new();
        const string text = "Hello, ^^xyz world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Hello, xyz world!", filtered);
    }

    [Fact]
    public void Apply_Match_Changed()
    {
        Iso639TextFilter filter = new();
        const string text = "Hello, ^^eng and ^^ita world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Hello, English and Italian world!", filtered);
    }

    [Fact]
    public void Apply_Match2Letters_Changed()
    {
        Iso639TextFilter filter = new();
        filter.Configure(new Iso639FilterOptions
        {
            TwoLetters = true,
            Pattern = @"\^\^([a-z]{2})"
        });
        const string text = "Hello, ^^en and ^^it world!";

        string? filtered = filter.Apply(text)?.ToString();

        Assert.Equal("Hello, English and Italian world!", filtered);
    }
}
