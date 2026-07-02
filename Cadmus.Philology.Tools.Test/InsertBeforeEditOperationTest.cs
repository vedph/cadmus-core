using System;

namespace Cadmus.Philology.Tools.Test;

public sealed class InsertBeforeEditOperationTest
{
    [Fact]
    public void Execute_InsertAtStart()
    {
        InsertBeforeEditOperation op = new()
        {
            At = 0,
            Text = "abc"
        };
        const string a = "xyz";

        string b = op.Execute(a);

        Assert.Equal("abcxyz", b);
    }

    [Fact]
    public void Execute_InsertInMiddle()
    {
        InsertBeforeEditOperation op = new()
        {
            At = 2,
            Text = "abc"
        };
        const string a = "xyz";

        string b = op.Execute(a);

        Assert.Equal("xabcyz", b);
    }

    [Fact]
    public void Execute_InsertAtEnd()
    {
        InsertBeforeEditOperation op = new()
        {
            At = 3,
            Text = "X"
        };
        const string a = "abc";

        string b = op.Execute(a);

        Assert.Equal("abXc", b);
    }

    [Fact]
    public void Execute_InvalidPosition_Throws()
    {
        InsertBeforeEditOperation op = new()
        {
            At = 10,
            Text = "abc"
        };
        const string a = "xyz";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Parse_ValidFormat()
    {
        InsertBeforeEditOperation op = new();
        op.Parse("@2+=\"abc\"");

        Assert.Equal(2, op.At);
        Assert.Equal("abc", op.Text);
    }

    [Fact]
    public void Parse_ValidFormat_ZeroPosition()
    {
        InsertBeforeEditOperation op = new();
        op.Parse("@0+=\"abc\"");

        Assert.Equal(0, op.At);
        Assert.Equal("abc", op.Text);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        InsertBeforeEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_NegativePosition_Throws()
    {
        InsertBeforeEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@-1+=\"abc\""));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        InsertBeforeEditOperation op = new()
        {
            At = 2,
            Text = "abc"
        };

        string s = op.ToString();

        Assert.Equal("@2+=\"abc\"", s);
    }

    [Fact]
    public void ToString_WithNoteAndTags()
    {
        InsertBeforeEditOperation op = new()
        {
            At = 2,
            Text = "abc",
            Note = "note"
        };
        op.Tags.Add("t1");
        op.Tags.Add("t2");

        string s = op.ToString();

        Assert.Equal("@2+=\"abc\" [t1 t2] {note}", s);
    }
}