using System;
using Xunit;

namespace Cadmus.Philology.Tools.Test;

public sealed class SwapEditOperationTest
{
    [Fact]
    public void Execute_SwapSingleChars()
    {
        SwapEditOperation op = new()
        {
            At = 2,
            Run = 1,
            At2 = 4,
            Run2 = 1
        };
        const string a = "abcde";

        string b = op.Execute(a);

        // swap 'b' (2) and 'd' (4): a d c b e
        Assert.Equal("adcbe", b);
    }

    [Fact]
    public void Execute_SwapMultiChars()
    {
        SwapEditOperation op = new()
        {
            At = 2,
            Run = 2,
            At2 = 5,
            Run2 = 2
        };
        const string a = "abcdefg";

        string b = op.Execute(a);

        // swap "bc" (2-3) and "ef" (5-6): a e f d b c g
        Assert.Equal("aefdbcg", b);
    }

    [Fact]
    public void Execute_Overlapping_Throws()
    {
        SwapEditOperation op = new()
        {
            At = 2,
            Run = 3,
            At2 = 4,
            Run2 = 2
        };
        const string a = "abcdefg";

        Assert.Throws<ArgumentException>(() => op.Execute(a));
    }

    [Fact]
    public void Execute_InvalidPosition_Throws()
    {
        SwapEditOperation op = new()
        {
            At = 0,
            Run = 1,
            At2 = 3,
            Run2 = 1
        };
        const string a = "abc";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Execute_InvalidSecondPosition_Throws()
    {
        SwapEditOperation op = new()
        {
            At = 1,
            Run = 1,
            At2 = 0,
            Run2 = 1
        };
        const string a = "abc";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Parse_QuotedText_Positions_Lengths()
    {
        SwapEditOperation op = new();
        op.Parse("\"abc\"@2x2<>\"def\"@5x2");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal("def", op.InputText2);
        Assert.Equal(5, op.At2);
        Assert.Equal(2, op.Run2);
    }

    [Fact]
    public void Parse_Positions_Only()
    {
        SwapEditOperation op = new();
        op.Parse("@2<>@4");

        Assert.Null(op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
        Assert.Null(op.InputText2);
        Assert.Equal(4, op.At2);
        Assert.Equal(1, op.Run2);
    }

    [Fact]
    public void Parse_Positions_Lengths_x()
    {
        SwapEditOperation op = new();
        op.Parse("@2x2<>@5x2");

        Assert.Null(op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(2, op.Run);
        Assert.Null(op.InputText2);
        Assert.Equal(5, op.At2);
        Assert.Equal(2, op.Run2);
    }

    [Fact]
    public void Parse_Positions_Lengths_times()
    {
        SwapEditOperation op = new();
        op.Parse("@2×2<>@5×2");

        Assert.Null(op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(2, op.Run);
        Assert.Null(op.InputText2);
        Assert.Equal(5, op.At2);
        Assert.Equal(2, op.Run2);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        SwapEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_InvalidFirstPosition_Throws()
    {
        SwapEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@0<>@2"));
    }

    [Fact]
    public void Parse_InvalidFirstLength_Throws()
    {
        SwapEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2x0<>@3"));
    }

    [Fact]
    public void Parse_InvalidSecondPosition_Throws()
    {
        SwapEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2<>@0"));
    }

    [Fact]
    public void Parse_InvalidSecondLength_Throws()
    {
        SwapEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2<>@3x0"));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        SwapEditOperation op = new()
        {
            At = 2,
            Run = 2,
            At2 = 5,
            Run2 = 2
        };

        string s = op.ToString();

        Assert.Equal("@2x2<>@5x2", s);
    }

    [Fact]
    public void ToString_WithInputText_Note_Tags()
    {
        SwapEditOperation op = new()
        {
            InputText = "abc",
            At = 2,
            Run = 2,
            InputText2 = "def",
            At2 = 5,
            Run2 = 2,
            Note = "note"
        };
        op.Tags.Add("t1");
        op.Tags.Add("t2");

        string s = op.ToString();

        Assert.Equal("\"abc\"@2x2<>\"def\"@5x2 [t1 t2] {note}", s);
    }
}