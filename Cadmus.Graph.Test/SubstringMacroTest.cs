using Cadmus.Export.Mapping;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class SubstringMacroTest
{
    [Fact]
    public void Run_NullArgs_ReturnsNull()
    {
        SubstringMacro macro = new();

        Assert.Null(macro.Run(null, null));
    }

    [Fact]
    public void Run_EmptyArgs_ReturnsNull()
    {
        SubstringMacro macro = new();

        Assert.Null(macro.Run(null, []));
    }

    [Fact]
    public void Run_OnlyString_ReturnsWholeString()
    {
        SubstringMacro macro = new();

        string? result = macro.Run(null, ["hello"]);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Run_StringAndStart_ReturnsSubstringFromStart()
    {
        SubstringMacro macro = new();

        string? result = macro.Run(null, ["hello", "2"]);

        Assert.Equal("llo", result);
    }

    [Fact]
    public void Run_StringStartAndLength_ReturnsSubstring()
    {
        SubstringMacro macro = new();

        string? result = macro.Run(null, ["hello", "1", "3"]);

        Assert.Equal("ell", result);
    }
}
