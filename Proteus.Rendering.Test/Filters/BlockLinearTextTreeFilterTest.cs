using Fusi.Tools.Data;
using Proteus.Rendering.Filters;
using System.Collections.Generic;
using System.Linq;

namespace Proteus.Rendering.Test.Filters;

public sealed class BlockLinearTextTreeFilterTest
{
    [Fact]
    public void Apply_NoLF_Unchanged()
    {
        // create a tree with segments that don't contain LF
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree(
            "Hello", "world!");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        List<ExportedSegment?> orSegments = [];
        tree.Traverse(node =>
        {
            orSegments.Add(node.Data);
            return true;
        });
        List<ExportedSegment?> filteredSegments = [];
        filtered.Traverse(node =>
        {
            filteredSegments.Add(node.Data);
            return true;
        });

        // must be equal
        Assert.Equal(orSegments.Count, filteredSegments.Count);
        for (int i = 0; i < orSegments.Count; i++)
        {
            TestHelper.AssertSegmentsEqual(orSegments[i], filteredSegments[i]);
        }
    }

    [Fact]
    public void Apply_LFOnly_MarksParentWithFeature()
    {
        // create a tree with a single node containing only LF
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree("\n");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // the root should get the EOL_TAIL feature
        Assert.NotNull(filtered.Data);
        Assert.True(filtered.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
        // LF node was removed
        Assert.Empty(filtered.Children);
    }

    [Fact]
    public void Apply_MultipleLFOnly_MarksParentWithFeature()
    {
        // create a tree with nodes containing only LFs
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree("\n", "\n", "\n");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // the root should get the EOL_TAIL feature
        Assert.NotNull(filtered.Data);
        Assert.True(filtered.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
        // all LF nodes were removed
        Assert.Empty(filtered.Children);
    }

    [Fact]
    public void Apply_LFAtStart_MarksParentAndHandlesRemainingText()
    {
        // create a tree with LF at start followed by text
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree("\n", "Hello");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // root should be marked with EOL_TAIL feature
        Assert.NotNull(filtered.Data);
        Assert.True(filtered.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // Hello segment should still be present
        Assert.Single(filtered.Children);
        Assert.Equal("Hello", filtered.FirstChild!.Data!.Text);
        Assert.False(filtered.FirstChild!.Data!.HasFeature(
            ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_LFAtEnd_SplitsNode()
    {
        // create a tree with text followed by LF at end
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree("Hello", "\n");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // Hello node should remain and be marked with EOL_TAIL
        Assert.Single(filtered.Children);
        Assert.Equal("Hello", filtered.FirstChild!.Data!.Text);
        Assert.True(filtered.FirstChild!.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // LF node should be removed
        Assert.Empty(filtered.FirstChild!.Children);
    }

    [Fact]
    public void Apply_LFInMiddle_SplitsNode()
    {
        // create a tree with text, LF, and more text
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree(
            "Hello", "\n", "World");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // first segment (Hello) should be marked with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Hello", firstNode.Data!.Text);
        Assert.True(firstNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // next segment (World) should be a child of the first segment
        Assert.Single(firstNode.Children);
        TreeNode<ExportedSegment> secondNode = firstNode.FirstChild!;
        Assert.Equal("World", secondNode.Data!.Text);
        Assert.False(secondNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_NodeContainingLF_SplitsNode()
    {
        // create a tree with a single node containing an LF
        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = new(
            new ExportedSegment { Text = "Hello\nWorld" });
        root.AddChild(node);

        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> filtered = filter.Apply(root);

        // node should be split into two nodes
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Hello", firstNode.Data!.Text);
        Assert.True(firstNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        Assert.Single(firstNode.Children);
        TreeNode<ExportedSegment> secondNode = firstNode.FirstChild!;
        Assert.Equal("World", secondNode.Data!.Text);
        Assert.False(secondNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_MultipleLFInMiddle_SplitsNodeMultipleTimes()
    {
        // create a tree with multiple segments and LFs
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree(
            "Hello", "\n", "Beautiful", "\n", "World");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // first level: "Hello" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> hello = filtered.FirstChild!;
        Assert.Equal("Hello", hello.Data!.Text);
        Assert.True(hello.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // second level: "Beautiful" with EOL_TAIL
        Assert.Single(hello.Children);
        TreeNode<ExportedSegment> beautiful = hello.FirstChild!;
        Assert.Equal("Beautiful", beautiful.Data!.Text);
        Assert.True(beautiful.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // third level: "World" without EOL_TAIL
        Assert.Single(beautiful.Children);
        TreeNode<ExportedSegment> world = beautiful.FirstChild!;
        Assert.Equal("World", world.Data!.Text);
        Assert.False(world.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_NodeWithMultipleInternalLFs_SplitsCorrectly()
    {
        // create a tree with a node containing multiple LFs internally
        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = new(new ExportedSegment
        {
            Text = "First\nSecond\nThird"
        });
        root.AddChild(node);

        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> filtered = filter.Apply(root);

        // first level: "First" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> first = filtered.FirstChild!;
        Assert.Equal("First", first.Data!.Text);
        Assert.True(first.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // second level: "Second" with EOL_TAIL
        Assert.Single(first.Children);
        TreeNode<ExportedSegment> second = first.FirstChild!;
        Assert.Equal("Second", second.Data!.Text);
        Assert.True(second.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // third level: "Third" without EOL_TAIL
        Assert.Single(second.Children);
        TreeNode<ExportedSegment> third = second.FirstChild!;
        Assert.Equal("Third", third.Data!.Text);
        Assert.False(third.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_LFAtStartAndMiddle_HandlesBothCases()
    {
        // create a tree with LF at start and in middle
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree(
            "\n", "Hello", "\n", "World");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // root should have EOL_TAIL feature
        Assert.NotNull(filtered.Data);
        Assert.True(filtered.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // first level: "Hello" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Hello", firstNode.Data!.Text);
        Assert.True(firstNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // second level: "World" without EOL_TAIL
        Assert.Single(firstNode.Children);
        TreeNode<ExportedSegment> secondNode = firstNode.FirstChild!;
        Assert.Equal("World", secondNode.Data!.Text);
        Assert.False(secondNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_LFAtEndAndMiddle_SplitsNodesCorrectly()
    {
        // create a tree with LF in middle and at end
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree(
            "Hello", "\n", "World", "\n");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // first level: "Hello" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Hello", firstNode.Data!.Text);
        Assert.True(firstNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // second level: "World" with EOL_TAIL
        Assert.Single(firstNode.Children);
        TreeNode<ExportedSegment> secondNode = firstNode.FirstChild!;
        Assert.Equal("World", secondNode.Data!.Text);
        Assert.True(secondNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // there should be no third level (as the last LF is at the end)
        Assert.Empty(secondNode.Children);
    }

    [Fact]
    public void Apply_NodeWithLFAtStartAndEnd_SplitsCorrectly()
    {
        // create a tree with a node containing LF at start and end
        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = new(new ExportedSegment
        {
            Text = "\nMiddle\n"
        });
        root.AddChild(node);

        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> filtered = filter.Apply(root);

        // root should have EOL_TAIL feature
        Assert.NotNull(filtered.Data);
        Assert.True(filtered.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // first level: "Middle" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Middle", firstNode.Data!.Text);
        Assert.True(firstNode.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // no further children
        Assert.Empty(firstNode.Children);
    }

    [Fact]
    public void Apply_ComplexLFScenario_HandlesAllLFs()
    {
        // create a tree with a complex combination of LFs
        TreeNode<ExportedSegment> tree = TestHelper.GetLinearTree(
            "\n", "First", "\n", "Second", "\n", "\n", "Third", "\n");
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // root should have EOL_TAIL feature
        Assert.NotNull(filtered.Data);
        Assert.True(filtered.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // first level: "First" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> first = filtered.FirstChild!;
        Assert.Equal("First", first.Data!.Text);
        Assert.True(first.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // second level: "Second" with EOL_TAIL
        Assert.Single(first.Children);
        TreeNode<ExportedSegment> second = first.FirstChild!;
        Assert.Equal("Second", second.Data!.Text);
        Assert.True(second.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // third level: "Third" with EOL_TAIL
        Assert.Single(second.Children);
        TreeNode<ExportedSegment> third = second.FirstChild!;
        Assert.Equal("Third", third.Data!.Text);
        Assert.True(third.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // No fifth level (as the last LF is at the end)
        Assert.Empty(third.Children);
    }

    [Fact]
    public void Apply_PreservesNodeProperties()
    {
        // create a tree with custom node properties
        TreeNode<ExportedSegment> node = new(new ExportedSegment("Hello\nWorld"))
        {
            Id = "123",
            Label = "TestNode",
            IsExpanded = true
        };
        node.Data!.AddFeature("custom", "value");
        node.Data.SourceId = 42;
        node.Data.Type = "test";

        TreeNode<ExportedSegment> tree = new();
        tree.AddChild(node);

        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // verify that node properties are preserved in the split nodes
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Hello", firstNode.Data!.Text);
        Assert.Equal("test", firstNode.Data.Type);
        Assert.Equal(42, firstNode.Data.SourceId);
        Assert.True(firstNode.Data.HasFeature("custom", "value"));

        // second node should also preserve properties
        Assert.Single(firstNode.Children);
        TreeNode<ExportedSegment> secondNode = firstNode.FirstChild!;
        Assert.Equal("World", secondNode.Data!.Text);
        Assert.Equal("test", secondNode.Data.Type);
        Assert.Equal(42, secondNode.Data.SourceId);
        Assert.True(secondNode.Data.HasFeature("custom", "value"));
    }

    [Fact]
    public void Apply_ConsecutiveLF_HandlesCorrectly()
    {
        // tree with consecutive LFs
        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = new(new ExportedSegment
        {
            Text = "Hello\n\n\nWorld!"
        });
        root.AddChild(node);

        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> filtered = filter.Apply(root);

        // first level: "Hello" with EOL_TAIL
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> hello = filtered.FirstChild!;
        Assert.Equal("Hello", hello.Data!.Text);
        Assert.True(hello.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));

        // second level: "World!" without EOL_TAIL
        Assert.Single(hello.Children);
        TreeNode<ExportedSegment> world = hello.FirstChild!;
        Assert.Equal("World!", world.Data!.Text);
        Assert.False(world.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_PreservesNodeFeatures()
    {
        // create a tree with custom node features
        TreeNode<ExportedSegment> node = new(new ExportedSegment("Hello\nWorld"));
        node.Data!.AddFeature("custom", "value");
        node.Data.AddFeature("another", "feature");
        TreeNode<ExportedSegment> tree = new();
        tree.AddChild(node);
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // verify that node features are preserved in the split nodes
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> firstNode = filtered.FirstChild!;
        Assert.Equal("Hello", firstNode.Data!.Text);
        Assert.True(firstNode.Data!.HasFeature("custom", "value"));
        Assert.True(firstNode.Data.HasFeature("another", "feature"));

        // second node should also preserve features
        Assert.Single(firstNode.Children);
        TreeNode<ExportedSegment> secondNode = firstNode.FirstChild!;
        Assert.Equal("World", secondNode.Data!.Text);
        Assert.True(secondNode.Data!.HasFeature("custom", "value"));
        Assert.True(secondNode.Data.HasFeature("another", "feature"));
    }

    [Fact]
    public void Apply_PreservesNodeIdAndLabels()
    {
        // create a tree with custom node IDs and labels
        TreeNode<ExportedSegment> node = new(new ExportedSegment("Hello\nWorld"))
        {
            Id = "1",
            Label = "Hello\nWorld"
        };
        TreeNode<ExportedSegment> tree = new();
        tree.AddChild(node);
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> filtered = filter.Apply(tree);

        // verify that node IDs and labels are preserved in the split nodes
        // Hello
        Assert.Single(filtered.Children);
        TreeNode<ExportedSegment> hello = filtered.FirstChild!;
        Assert.Equal("Hello", hello.Data!.Text);
        Assert.Equal("1", hello.Id);
        Assert.Equal("Hello\nWorld", hello.Label);
        Assert.True(hello.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
        // World
        Assert.Single(hello.Children);
        TreeNode<ExportedSegment> world = hello.FirstChild!;
        Assert.Equal("World", world.Data!.Text);
        Assert.Equal("1", world.Id);
        Assert.Equal("Hello\nWorld", world.Label);
        Assert.False(world.Data!.HasFeature(ExportedSegment.F_EOL_TAIL));
        Assert.False(world.HasChildren);
    }
}
