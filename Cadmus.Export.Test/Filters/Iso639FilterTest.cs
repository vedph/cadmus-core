using Cadmus.Export.Filters;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class Iso639FilterTest
{
    [Fact]
    public async Task Apply_NoMatch_Unchanged()
    {
        Iso639TextFilter filter = new();
        const string text = "Hello, world!";

        string? filtered = (await filter.ApplyAsync(text))?.ToString();

        Assert.Equal(text, filtered);
    }

    [Fact]
    public async Task Apply_MatchInvalidCode_Code()
    {
        Iso639TextFilter filter = new();
        const string text = "Hello, ^^xyz world!";

        string? filtered = (await filter.ApplyAsync(text))?.ToString();

        Assert.Equal("Hello, xyz world!", filtered);
    }

    [Fact]
    public async Task Apply_Match_Changed()
    {
        Iso639TextFilter filter = new();
        const string text = "Hello, ^^eng and ^^ita world!";

        string? filtered = (await filter.ApplyAsync(text))?.ToString();

        Assert.Equal("Hello, English and Italian world!", filtered);
    }

    [Fact]
    public async Task Apply_Match2Letters_Changed()
    {
        Iso639TextFilter filter = new();
        filter.Configure(new Iso639FilterOptions
        {
            TwoLetters = true,
            Pattern = @"\^\^([a-z]{2})"
        });
        const string text = "Hello, ^^en and ^^it world!";

        string? filtered = (await filter.ApplyAsync(text))?.ToString();

        Assert.Equal("Hello, English and Italian world!", filtered);
    }
}
