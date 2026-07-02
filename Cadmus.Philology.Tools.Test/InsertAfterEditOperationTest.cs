using Xunit;
using System;

namespace Cadmus.Philology.Tools.Test;

public sealed class InsertAfterEditOperationTest
{
    [Fact]
    public void Execute_InsertAtStart()
    {
        InsertAfterEditOperation op = new()
        {
            At = 0,
            Text = "abc"
        };
        const string a = "xyz";

        string b = op.Execute(a);

        Assert.Equal("xyzabc", b);
    }

    [Fact]
    public void Execute_InsertInMiddle()
    {
        InsertAfterEditOperation op = new()
        {
            At = 2,
            Text = "abc"
        };
        const string a = "xyz";

        string b = op.Execute(a);

        Assert.Equal("xyabcz", b);
    }

    [Fact]
    public void Execute_InsertAtEnd()
    {
        InsertAfterEditOperation op = new()
        {
            At = 3,
            Text = "abc"
        };
        const string a = "xyz";

        string b = op.Execute(a);

        Assert.Equal("xyzabc", b);
    }

    [Fact]
    public void Execute_NullInput_Throws()
    {
        InsertAfterEditOperation op = new()
        {
            At = 1,
            Text = "abc"
        };

        Assert.Throws<ArgumentNullException>(() => op.Execute(null!));
    }

    [Fact]
    public void Execute_InvalidPosition_Throws()
    {
        InsertAfterEditOperation op = new()
        {
            At = 5,
            Text = "abc"
        };
        const string a = "xyz";

        Assert.Throws<ArgumentOutOfRangeException>(() => op.Execute(a));
    }

    [Fact]
    public void Parse_ValidFormat()
    {
        InsertAfterEditOperation op = new();
        op.Parse("@2=+\"abc\"");

        Assert.Equal(2, op.At);
        Assert.Equal("abc", op.Text);
    }

    [Fact]
    public void Parse_ValidFormat_ZeroPosition()
    {
        InsertAfterEditOperation op = new();
        op.Parse("@0=+\"abc\"");

        Assert.Equal(0, op.At);
        Assert.Equal("abc", op.Text);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        InsertAfterEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_NegativePosition_Throws()
    {
        InsertAfterEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@-1=+\"abc\""));
    }

    [Fact]
    public void ToString_ReturnsExpected()
    {
        InsertAfterEditOperation op = new()
        {
            At = 2,
            Text = "abc"
        };

        string s = op.ToString();

        Assert.Equal("@2=+\"abc\"", s);
    }

    [Fact]
    public void ToString_WithNoteAndTags()
    {
        InsertAfterEditOperation op = new()
        {
            At = 2,
            Text = "abc",
            Note = "note"
        };
        op.Tags.Add("t1");
        op.Tags.Add("t2");

        string s = op.ToString();

        Assert.Equal("@2=+\"abc\" [t1 t2] {note}", s);
    }
}