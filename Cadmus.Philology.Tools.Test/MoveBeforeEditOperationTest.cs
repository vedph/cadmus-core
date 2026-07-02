using System;
using Xunit;

namespace Cadmus.Philology.Tools.Test;

public sealed class MoveBeforeEditOperationTest
{
    [Fact]
    public void Execute_MoveSingleChar_Forward()
    {
        MoveBeforeEditOperation op = new()
        {
            At = 2,
            Run = 1,
            To = 4
        };
        const string a = "abcde";

        string b = op.Execute(a);

        // move 'b' (at 2) before position 4: a c b d e
        Assert.Equal("acbde", b);
    }

    [Fact]
    public void Execute_MoveSingleChar_Backward()
    {
        MoveBeforeEditOperation op = new()
        {
            At = 4,
            Run = 1,
            To = 1
        };
        const string a = "abcde";

        string b = op.Execute(a);

        // move 'd' (at 4) before position 1: d a b c e
        Assert.Equal("dabce", b);
    }

    [Fact]
    public void Execute_MoveMultiChar_Forward()
    {
        MoveBeforeEditOperation op = new()
        {
            At = 2,
            Run = 2,
            To = 5
        };
        const string a = "abcdef";

        string b = op.Execute(a);

        // move "bc" (at 2, len 2) before position 5: a d b c e f
        Assert.Equal("adbcef", b);
    }

    [Fact]
    public void Execute_MoveMultiChar_Backward()
    {
        MoveBeforeEditOperation op = new()
        {
            At = 4,
            Run = 2,
            To = 1
        };
        const string a = "abcdef";

        string b = op.Execute(a);

        // move "de" (at 4, len 2) before position 1: d e a b c f
        Assert.Equal("deabcf", b);
    }

    [Fact]
    public void Execute_InvalidPosition_Throws()
    {
        MoveBeforeEditOperation op = new()
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
        MoveBeforeEditOperation op = new()
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
        MoveBeforeEditOperation op = new();
        op.Parse("\"abc\"@2>@4");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal(4, op.To);
    }

    [Fact]
    public void Parse_QuotedText_Position_Length_Target()
    {
        MoveBeforeEditOperation op = new();
        op.Parse("\"abc\"@2x3>@4");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(3, op.Run);
        Assert.Equal(4, op.To);
    }

    [Fact]
    public void Parse_Position_Only()
    {
        MoveBeforeEditOperation op = new();
        op.Parse("@4>@2");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(1, op.Run);
        Assert.Equal(2, op.To);
    }

    [Fact]
    public void Parse_Position_Length_x()
    {
        MoveBeforeEditOperation op = new();
        op.Parse("@4x2>@2");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal(2, op.To);
    }

    [Fact]
    public void Parse_Position_Length_times()
    {
        MoveBeforeEditOperation op = new();
        op.Parse("@4×2>@2");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
        Assert.Equal(2, op.To);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        MoveBeforeEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_InvalidPosition_Throws()
    {
        MoveBeforeEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@0>@2"));
    }

    [Fact]
    public void Parse_InvalidLength_Throws()
    {
        MoveBeforeEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2x0>@3"));
    }

    [Fact]
    public void Parse_InvalidTargetPosition_Throws()
    {
        MoveBeforeEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2>@0"));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        MoveBeforeEditOperation op = new()
        {
            At = 2,
            Run = 3,
            To = 5
        };

        string s = op.ToString();

        Assert.Equal("@2x3>@5", s);
    }

    [Fact]
    public void ToString_WithInputText_Note_Tags()
    {
        MoveBeforeEditOperation op = new()
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

        Assert.Equal("\"abc\"@2x3>@5 [t1 t2] {note}", s);
    }
}