using System;
using Xunit;

namespace Cadmus.Philology.Tools.Test;

public sealed class MoveAfterEditOperationTest
{
    [Fact]
    public void Execute_MoveSingleChar_Forward()
    {
        MoveAfterEditOperation op = new()
        {
            At = 2,
            Run = 1,
            To = 4
        };
        const string a = "abcde";

        string b = op.Execute(a);

        // move 'b' (at 2) after position 4: a c d b e
        Assert.Equal("acdbe", b);
    }

    [Fact]
    public void Execute_MoveSingleChar_Backward()
    {
        MoveAfterEditOperation op = new()
        {
            At = 4,
            Run = 1,
            To = 1
        };
        const string a = "abcde";

        string b = op.Execute(a);

        // move 'd' (at 4) after position 1: a d b c e
        Assert.Equal("adbc e".Replace(" ", ""), b);
    }

    [Fact]
    public void Execute_MoveMultiChar_Forward()
    {
        MoveAfterEditOperation op = new()
        {
            At = 2,
            Run = 2,
            To = 5
        };
        const string a = "abcdef";

        string b = op.Execute(a);

        // move "bc" (at 2, len 2) after position 5: a d e b c f
        Assert.Equal("adebcf", b);
    }

    [Fact]
    public void Execute_MoveMultiChar_Backward()
    {
        MoveAfterEditOperation op = new()
        {
            At = 4,
            Run = 2,
            To = 1
        };
        const string a = "abcdef";

        string b = op.Execute(a);

        // move "de" (at 4, len 2) after position 1: a d e b c f
        Assert.Equal("adebcf", b);
    }

    [Fact]
    public void Execute_InvalidPosition_Throws()
    {
        MoveAfterEditOperation op = new()
        {
            At = 0,
            Run = 1,
            To = 2
        };
        const string a = "abc";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Execute_InvalidTargetPosition_Throws()
    {
        MoveAfterEditOperation op = new()
        {
            At = 1,
            Run = 1,
            To = 0
        };
        const string a = "abc";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Parse_QuotedText_Position_Target()
    {
        MoveAfterEditOperation op = new();
        op.Parse("\"abc\"@2->@4");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal(4, op.To);
    }

    [Fact]
    public void Parse_QuotedText_Position_Length_Target()
    {
        MoveAfterEditOperation op = new();
        op.Parse("\"abc\"@2x3->@4");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(3, op.Run);
        Assert.Equal(4, op.To);
    }

    [Fact]
    public void Parse_Position_Only()
    {
        MoveAfterEditOperation op = new();
        op.Parse("@4->@2");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal(2, op.To);
    }

    [Fact]
    public void Parse_Position_Length_x()
    {
        MoveAfterEditOperation op = new();
        op.Parse("@4x2->@2");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal(2, op.To);
    }

    [Fact]
    public void Parse_Position_Length_times()
    {
        MoveAfterEditOperation op = new();
        op.Parse("@4×2->@2");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal(2, op.To);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        MoveAfterEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_InvalidPosition_Throws()
    {
        MoveAfterEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@0->@2"));
    }

    [Fact]
    public void Parse_InvalidLength_Throws()
    {
        MoveAfterEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2x0->@3"));
    }

    [Fact]
    public void Parse_InvalidTargetPosition_Throws()
    {
        MoveAfterEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2->@0"));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        MoveAfterEditOperation op = new()
        {
            At = 2,
            Run = 3,
            To = 5
        };

        string s = op.ToString();

        Assert.Equal("@2x3->@5", s);
    }

    [Fact]
    public void ToString_WithInputText_Note_Tags()
    {
        MoveAfterEditOperation op = new()
        {
            InputText = "abc",
            At = 2,
            Run = 3,
            To = 5,
            Note = "note"
        };
        op.Tags.Add("t1");
        op.Tags.Add("t2");

        string s = op.ToString();

        Assert.Equal("\"abc\"@2x3->@5 [t1 t2] {note}", s);
    }
}