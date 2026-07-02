using Fusi.Tools;
using Fusi.Tools.Data;
using System;

namespace Proteus.Rendering.Test;

internal static class TestHelper
{
    public static TreeNode<ExportedSegment> GetLinearTree(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = root;

        foreach (char c in text)
        {
            TreeNode<ExportedSegment> child = new(new ExportedSegment());
            child.Data!.Text = c.ToString();
            node.AddChild(child);
            node = child;
        }

        return root;
    }

    public static TreeNode<ExportedSegment> GetLinearTree(
        params string[] segments)
    {
        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = root;

        foreach (string segment in segments)
        {
            TreeNode<ExportedSegment> child = new(new ExportedSegment());
            child.Data!.Text = segment;
            node.AddChild(child);
            node = child;
        }

        return root;
    }

    public static void AssertSegmentsEqual(ExportedSegment? a, ExportedSegment? b)
    {
        if (a == null)
        {
            Assert.Null(b);
            return;
        }
        if (b == null)
        {
            Assert.Null(a);
            return;
        }

        Assert.Equal(a.Text, b.Text);
        Assert.Equal(a.Features?.Count, b.Features?.Count);
        Assert.Equal(a.Tags, b.Tags);
        Assert.Equal(a.Payloads?.Count, b.Payloads?.Count);

        if (a.Features != null)
        {
            foreach (StringPair feature in a.Features)
                Assert.Contains(feature, b.Features!);
        }
        if (a.Payloads != null)
        {
            foreach (object payload in a.Payloads)
                Assert.Contains(payload, b.Payloads!);
        }
    }
}
