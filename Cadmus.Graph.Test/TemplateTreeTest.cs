using System;
using System.Text;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class TemplateTreeTest
{
    private static string MockResolver(TemplateNode node)
    {
        if (node.Children.Count == 0) return "";

        StringBuilder sb = new();
        foreach (TemplateNode child in node.Children)
            sb.Append(child.Value ?? "");
        return sb.ToString().ToLowerInvariant();
    }

    [Fact]
    public void Create_Ok()
    {
        // -R
        // --A
        // --X
        // ---BC
        // ---X
        // ----D
        // --E
        // --X
        // ---F
        // --G
        const string template = "A{$BC{$D}}E{$F}G";
        TemplateTree tree = TemplateTree.Create(template);

        TemplateNode root = tree.Root;

        // R/ A,X,E,X,G
        Assert.Equal(5, root.ChildrenCount);
        Assert.Equal("A", root.Children[0].Value);
        Assert.Null(root.Children[1].Value);
        Assert.Equal("E", root.Children[2].Value);
        Assert.Null(root.Children[3].Value);
        Assert.Equal("G", root.Children[4].Value);

        // R/X/ BC,X
        TemplateNode node = root.Children[1];
        Assert.Equal(2, node.ChildrenCount);
        Assert.Equal("BC", node.Children[0].Value);
        Assert.Null(node.Children[1].Value);

        // R/X/X/ D
        node = node.Children[1];
        Assert.Equal(1, node.ChildrenCount);
        Assert.Equal("D", node.Children[0].Value);

        // R/X/ F
        node = root.Children[3];
        Assert.Equal(1, node.ChildrenCount);
        Assert.Equal("F", node.Children[0].Value);
    }

    [Fact]
    public void Resolve_Ok()
    {
        const string template = "A{$BC{$D}}E{$F}G";
        TemplateTree tree = TemplateTree.Create(template);

        string result = tree.Resolve(MockResolver);

        Assert.Equal("AbcdEfG", result);
    }

    [Fact]
    public void Create_LoneBackslash_DoesNotHang()
    {
        // a backslash not followed by { or } is not an escape sequence:
        // it must be treated as a literal character rather than stalling
        // the parser (regression test for an infinite loop)
        const string template = @"C:\path\to\file{$X}";
        TemplateTree tree = TemplateTree.Create(template);

        string result = tree.Resolve(MockResolver);

        Assert.Equal(@"C:\path\to\filex", result);
    }

    [Fact]
    public void Create_EscapedBraces_Ok()
    {
        const string template = @"\{literal\}";
        TemplateTree tree = TemplateTree.Create(template);

        string result = tree.Resolve(MockResolver);

        Assert.Equal("{literal}", result);
    }

    [Fact]
    public void Create_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => TemplateTree.Create(null!));
    }

    [Fact]
    public void Create_NoBraces_IsLiteralFastPath()
    {
        const string template = "just a literal string";

        TemplateTree tree = TemplateTree.Create(template);
        string result = tree.Resolve(MockResolver);

        Assert.Equal(template, result);
    }

    [Fact]
    public void Create_TrailingLoneOpenBrace_IsLiteral()
    {
        const string template = "abc{";

        TemplateTree tree = TemplateTree.Create(template);
        string result = tree.Resolve(MockResolver);

        Assert.Equal("abc{", result);
    }

    [Fact]
    public void Create_LoneCloseBrace_IsLiteral()
    {
        const string template = "abc}def";

        TemplateTree tree = TemplateTree.Create(template);
        string result = tree.Resolve(MockResolver);

        Assert.Equal("abc}def", result);
    }

    [Fact]
    public void Create_InvalidPlaceholderType_Throws()
    {
        const string template = "{Xinvalid}";

        Assert.Throws<CadmusGraphException>(() => TemplateTree.Create(template));
    }

    [Fact]
    public void Create_UnclosedBrace_Throws()
    {
        const string template = "{$unclosed";

        Assert.Throws<CadmusGraphException>(() => TemplateTree.Create(template));
    }

    [Fact]
    public void Resolve_NullResolver_Throws()
    {
        TemplateTree tree = TemplateTree.Create("literal");

        Assert.Throws<ArgumentNullException>(() => tree.Resolve(null!));
    }
}
