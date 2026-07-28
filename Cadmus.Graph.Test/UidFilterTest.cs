using Xunit;

namespace Cadmus.Graph.Test;

public sealed class UidFilterTest
{
    [Theory]
    [InlineData("", "_")]
    [InlineData("Hello, 1 World!", "hello_1_world")]
    [InlineData("ciáo MÓNDO", "ciao_mondo")]
    [InlineData("http://www.some-ontology/guys#543-21&x=1", "http://www.some-ontology/guys#543-21&x=1")]
    [InlineData("a.b?c=d%e", "a.b?c=d%e")]
    public void Filter_Ok(string text, string expected)
    {
        string actual = UidFilter.Apply(text);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Apply_Null_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => UidFilter.Apply(null!));
    }

    [Fact]
    public void Apply_DisableDirective_RemovesInitialBang()
    {
        Assert.Equal("Some Raw Text!", UidFilter.Apply("!Some Raw Text!"));
    }

    [Fact]
    public void Apply_OnlyBang_ReturnsUnderscore()
    {
        Assert.Equal("_", UidFilter.Apply("!"));
    }
}
