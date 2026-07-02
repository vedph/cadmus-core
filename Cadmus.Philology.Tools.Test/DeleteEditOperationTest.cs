using Xunit;

namespace Cadmus.Philology.Tools.Test;

public sealed class DeleteEditOperationTest
{
    [Fact]
    public void Execute_SingleChar_Middle()
    {
        DeleteEditOperation op = (DeleteEditOperation)
            EditOperation.ParseOperation("@3!")!;
        const string a = "coniunx";

        string b = op.Execute(a);

        Assert.Equal("coiunx", b);
    }

    [Fact]
    public void Execute_SingleChar_Start()
    {
        DeleteEditOperation op = (DeleteEditOperation)
            EditOperation.ParseOperation("@1!")!;
        const string a = "coniunx";

        string b = op.Execute(a);

        Assert.Equal("oniunx", b);
    }

    [Fact]
    public void Execute_SingleChar_End()
    {
        DeleteEditOperation op = (DeleteEditOperation)
            EditOperation.ParseOperation("@7!")!;
        const string a = "coniunx";

        string b = op.Execute(a);

        Assert.Equal("coniun", b);
    }

    [Fact]
    public void Execute_MultiChar_Middle()
    {
        DeleteEditOperation op = (DeleteEditOperation)
            EditOperation.ParseOperation("@3x2!")!;
        const string a = "coniunx";

        string b = op.Execute(a);

        Assert.Equal("counx", b);
    }

    [Fact]
    public void Execute_MultiChar_Start()
    {
        DeleteEditOperation op = (DeleteEditOperation)
            EditOperation.ParseOperation("@1x2!")!;
        const string a = "coniunx";

        string b = op.Execute(a);

        Assert.Equal("niunx", b);
    }

    [Fact]
    public void Execute_MultiChar_End()
    {
        DeleteEditOperation op = (DeleteEditOperation)
            EditOperation.ParseOperation("@6x2!")!;
        const string a = "coniunx";

        string b = op.Execute(a);

        Assert.Equal("coniu", b);
    }

    [Fact]
    public void Parse_Dsl_QuotedText_Position()
    {
        DeleteEditOperation op = new();
        op.Parse("\"abc\"@2!");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(1, op.Run);
    }

    [Fact]
    public void Parse_Dsl_QuotedText_Position_Length()
    {
        DeleteEditOperation op = new();
        op.Parse("\"abc\"@2x3!");

        Assert.Equal("abc", op.InputText);
        Assert.Equal(2, op.At);
        Assert.Equal(3, op.Run);
    }

    [Fact]
    public void Parse_Dsl_Position_Only()
    {
        DeleteEditOperation op = new();
        op.Parse("@4!");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(1, op.Run);
    }

    [Fact]
    public void Parse_Dsl_Position_Length_x()
    {
        DeleteEditOperation op = new();
        op.Parse("@4x2!");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
    }

    [Fact]
    public void Parse_Dsl_Position_Length_times()
    {
        DeleteEditOperation op = new();
        op.Parse("@4×2!");

        Assert.Null(op.InputText);
        Assert.Equal(4, op.At);
        Assert.Equal(2, op.Run);
    }

    [Fact]
    public void Parse_InvalidFormat_Throws()
    {
        DeleteEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("invalid!"));
    }

    [Fact]
    public void Parse_InvalidPosition_Throws()
    {
        DeleteEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@0!"));
    }

    [Fact]
    public void Parse_InvalidLength_Throws()
    {
        DeleteEditOperation op = new();
        Assert.Throws<ParseException>(() => op.Parse("@2x0!"));
    }
}
