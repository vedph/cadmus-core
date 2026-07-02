using System;
using Xunit;

namespace Cadmus.Philology.Tools.Test;

public sealed class ReplaceEditOperationTest
{
    [Fact]
    public void Execute_ReplaceSingleChar()
    {
        ReplaceEditOperation op = new()
        {
            At = 2,
            Run = 1,
            ReplacementText = "X"
        };
        const string a = "abcde";

        string b = op.Execute(a);

        // replace 'b' (at 2) with 'X': a X c d e
        Assert.Equal("aXcde", b);
    }

    [Fact]
    public void Execute_ReplaceMultiChar()
    {
        ReplaceEditOperation op = new()
        {
            At = 2,
            Run = 3,
            ReplacementText = "XY"
        };
        const string a = "abcdef";

        string b = op.Execute(a);

        // replace "bcd" (at 2, len 3) with "XY": a X Y e f
        Assert.Equal("aXYef", b);
    }

    [Fact]
    public void Execute_InvalidPosition_Throws()
    {
        ReplaceEditOperation op = new()
        {
            At = 0,
            Run = 1,
            ReplacementText = "X"
        };
        const string a = "abc";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Execute_InvalidLength_Throws()
    {
        ReplaceEditOperation op = new()
        {
            At = 2,
            Run = 0,
            ReplacementText = "X"
        };
        const string a = "abc";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Parse_QuotedText_Position_Replacement()
    {
        ReplaceEditOperation op = new();
        op.Parse("\"abc\"@2=\"X\"");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal("X", op.ReplacementText);
    }

    [Fact]
    public void Parse_QuotedText_Position_Length_Replacement()
    {
        ReplaceEditOperation op = new();
        op.Parse("\"abc\"@2x3=\"XY\"");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(3, op.Run);
        Assert.Equal("XY", op.ReplacementText);
    }

    [Fact]
    public void Parse_Position_Only()
    {
        ReplaceEditOperation op = new();
        op.Parse("@4=\"Z\"");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal("Z", op.ReplacementText);
    }

    [Fact]
    public void Parse_Position_Length_x()
    {
        ReplaceEditOperation op = new();
        op.Parse("@4x2=\"Z\"");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal("Z", op.ReplacementText);
    }

    [Fact]
    public void Parse_Position_Length_times()
    {
        ReplaceEditOperation op = new();
        op.Parse("@4×2=\"Z\"");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal("Z", op.ReplacementText);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        ReplaceEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_InvalidPosition_Throws()
    {
        ReplaceEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@0=\"X\""));
    }

    [Fact]
    public void Parse_InvalidLength_Throws()
    {
        ReplaceEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2x0=\"X\""));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        ReplaceEditOperation op = new()
        {
            At = 2,
            Run = 3,
            ReplacementText = "XY"
        };

        string s = op.ToString();

        Assert.Equal("@2x3=\"XY\"", s);
    }

    [Fact]
    public void ToString_WithInputText_Note_Tags()
    {
        ReplaceEditOperation op = new()
        {
            InputText = "abc",
            At = 2,
            Run = 3,
            ReplacementText = "XY",
            Note = "note"
        };
        op.Tags.Add("t1");
        op.Tags.Add("t2");

        string s = op.ToString();

        Assert.Equal("\"abc\"@2x3=\"XY\" [t1 t2] {note}", s);
    }
}