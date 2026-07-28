using System;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class TemplateNodeTest
{
    [Fact]
    public void Ctor_DefaultsToDepth1NoChildren()
    {
        TemplateNode node = new();

        Assert.Equal(1, node.Depth);
        Assert.Equal(0, node.ChildrenCount);
        Assert.Null(node.Parent);
    }

    [Fact]
    public void AddChild_SetsParentAndDepth()
    {
        TemplateNode root = new();
        TemplateNode child = new() { Value = "child" };

        root.AddChild(child);

        Assert.Same(root, child.Parent);
        Assert.Equal(2, child.Depth);
        Assert.Equal(1, root.ChildrenCount);
        Assert.Same(child, root.Children[0]);
    }

    [Fact]
    public void AddChild_Null_Throws()
    {
        TemplateNode root = new();

        Assert.Throws<ArgumentNullException>(() => root.AddChild(null!));
    }

    [Fact]
    public void GetSiblingNumber_RootIsOne()
    {
        TemplateNode root = new();

        Assert.Equal(1, root.GetSiblingNumber());
    }

    [Fact]
    public void GetSiblingNumber_ReturnsOneBasedIndex()
    {
        TemplateNode root = new();
        TemplateNode a = new();
        TemplateNode b = new();
        root.AddChild(a);
        root.AddChild(b);

        Assert.Equal(1, a.GetSiblingNumber());
        Assert.Equal(2, b.GetSiblingNumber());
    }

    [Fact]
    public void ReplaceWith_Root_Throws()
    {
        TemplateNode root = new();

        Assert.Throws<InvalidOperationException>(
            () => root.ReplaceWith(new TemplateNode()));
    }

    [Fact]
    public void ReplaceWith_Null_Throws()
    {
        TemplateNode root = new();
        TemplateNode child = new();
        root.AddChild(child);

        Assert.Throws<ArgumentNullException>(() => child.ReplaceWith(null!));
    }

    [Fact]
    public void ReplaceWith_ReplacesNodeInParentChildren()
    {
        TemplateNode root = new();
        TemplateNode child = new() { Value = "old" };
        root.AddChild(child);
        TemplateNode replacement = new() { Value = "new" };

        child.ReplaceWith(replacement);

        Assert.Same(replacement, root.Children[0]);
        Assert.Same(root, replacement.Parent);
        Assert.Null(child.Parent);
    }

    [Fact]
    public void Visit_VisitsSelfAndDescendants()
    {
        TemplateNode root = new() { Value = "root" };
        TemplateNode child = new() { Value = "child" };
        root.AddChild(child);

        int count = 0;
        root.Visit(n =>
        {
            count++;
            return true;
        });

        Assert.Equal(2, count);
    }

    [Fact]
    public void Visit_StopsWhenVisitorReturnsFalse()
    {
        TemplateNode root = new() { Value = "root" };
        TemplateNode child = new() { Value = "child" };
        root.AddChild(child);

        int count = 0;
        root.Visit(n =>
        {
            count++;
            return false;
        });

        Assert.Equal(1, count);
    }

    [Fact]
    public void Dump_ProducesNonEmptyText()
    {
        TemplateNode root = new() { Value = "root" };
        TemplateNode child = new() { Value = "child" };
        root.AddChild(child);

        string dump = root.Dump();

        Assert.Contains("root", dump);
        Assert.Contains("child", dump);
    }

    [Fact]
    public void ToString_IncludesTypeValueAndChildrenCount()
    {
        TemplateNode node = new()
        {
            Type = TemplateNodeType.Metadatum,
            Value = "x"
        };
        node.AddChild(new TemplateNode());

        string s = node.ToString();

        Assert.Contains("Metadatum", s);
        Assert.Contains("x", s);
        Assert.Contains("1", s);
    }
}
