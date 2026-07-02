using Cadmus.Core;
using Fusi.Tools.Data;
using Proteus.Rendering;
using Proteus.Rendering.Filters;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public class BlockLinearTextTreeFilterTests
{
    private static TreeNode<ExportedSegment> CreateTextNode(string text)
    {
        return new TreeNode<ExportedSegment>(
            new ExportedSegment(text, null,
                // we do not care about range here, just use 0-length
                new AnnotatedTextRange(0, text.Length))
                {
                    Text = text
                });
    }

    [Fact]
    public void Apply_SingleNodeNoNewline_ReturnsSameNode()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("Hello world"));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        Assert.Single(result.Children);
        Assert.Equal("Hello world", result.Children[0].Data!.Text);

        Assert.Empty(result.Children[0].Children);
    }

    [Fact]
    public void Apply_SingleNodeWithNewline_SplitsNode()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("Hello\nworld"));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // Hello\n
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> left = result.Children[0];
        Assert.Equal("Hello", left.Data!.Text);
        Assert.True(left.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // world
        Assert.Single(left.Children);
        TreeNode<ExportedSegment> right = left.Children[0];
        Assert.Equal("world", right.Data!.Text);
        Assert.False(right.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        Assert.Empty(right.Children);
    }

    [Fact]
    public void Apply_MultipleNewlines_SplitsNodes()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("Hello\nworld\nagain"));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // Hello\n
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> left = result.Children[0];
        Assert.Equal("Hello", left.Data!.Text);
        Assert.True(left.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // world\n
        Assert.Single(left.Children);
        TreeNode<ExportedSegment> middle = left.Children[0];
        Assert.Equal("world", middle.Data!.Text);
        Assert.True(middle.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // again
        Assert.Single(middle.Children);
        TreeNode<ExportedSegment> right = middle.Children[0];
        Assert.Equal("again", right.Data!.Text);
        Assert.False(right.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        Assert.Empty(right.Children);
    }

    [Fact]
    public void Apply_EndsWithNewline_SplitsNodes()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("Hello\nworld\n"));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // Hello\n
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> left = result.Children[0];
        Assert.Equal("Hello", left.Data!.Text);
        Assert.True(left.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // world\n
        Assert.Single(left.Children);
        TreeNode<ExportedSegment> right = left.Children[0];
        Assert.Equal("world", right.Data!.Text);
        Assert.True(right.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        Assert.Empty(right.Children);
    }

    [Fact]
    public void Apply_MultipleNodesWithNewlines_SplitsNodes()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("Hello\n"));
        TreeNode<ExportedSegment> child = CreateTextNode("world\nagain");
        root.AddChild(child);

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // Hello\n
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> left = result.Children[0];
        Assert.Equal("Hello", left.Data!.Text);
        Assert.True(left.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // world\n
        Assert.Single(left.Children);
        TreeNode<ExportedSegment> right = left.Children[0];
        Assert.Equal("world", right.Data!.Text);
        Assert.True(right.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // again
        Assert.Single(right.Children);
        TreeNode<ExportedSegment> rightChild = right.Children[0];
        Assert.Equal("again", rightChild.Data!.Text);
        Assert.False(rightChild.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        Assert.Empty(rightChild.Children);
    }

    [Fact]
    public void Apply_TextWithInitialNewline_EolAddedToParent()
    {
        BlockLinearTextTreeFilter filter = new();

        // create a tree where a node's text starts with a newline
        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node1 = CreateTextNode("First node");
        root.AddChild(node1);

        // create a node whose text starts with a newline
        TreeNode<ExportedSegment> node2 = CreateTextNode("\nSecond node");
        node1.AddChild(node2);

        // apply the filter
        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // validate result structure:
        // - root
        //   - First node (with .HasFeature(CadmusTextTreeBuilder.F_EOL_TAIL)=true)
        //     - Second node (without the leading newline)

        // check root structure
        Assert.Single(result.Children);

        // check first node
        TreeNode<ExportedSegment> firstNode = result.Children[0];
        Assert.Equal("First node", firstNode.Data!.Text);
        Assert.True(firstNode.Data.HasFeature(ExportedSegment.F_EOL_TAIL),
            "First node should be marked with .HasFeature(CadmusTextTreeBuilder.F_EOL_TAIL)");
        Assert.Single(firstNode.Children);

        // check second node (should have the leading newline removed)
        TreeNode<ExportedSegment> secondNode = firstNode.Children[0];
        Assert.Equal("Second node", secondNode.Data!.Text);
        Assert.False(secondNode.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // no more children
        Assert.Empty(secondNode.Children);
    }

    [Fact]
    public void Apply_TextWithInitialAndInternalNewline_SplitsNodes()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("\nHello\nworld"));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // root
        Assert.NotNull(result.Data);
        Assert.True(result.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // Hello\n
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> hello = result.Children[0];
        Assert.Equal("Hello", hello.Data!.Text);
        Assert.True(hello.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // world
        Assert.Single(hello.Children);
        TreeNode<ExportedSegment> world = hello.Children[0];
        Assert.Equal("world", world.Data!.Text);
        Assert.False(world.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        Assert.Empty(world.Children);
    }

    [Fact]
    public void Apply_EmptyText_ReturnsNodeUnchanged()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode(""));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        Assert.Single(result.Children);
        Assert.Equal("", result.Children[0].Data!.Text);
        Assert.Empty(result.Children[0].Children);
    }

    [Fact]
    public void Apply_ConsecutiveNewlines_SplitsNodes()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();
        root.AddChild(CreateTextNode("Hello\n\nworld"));

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // Hello\n
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> hello = result.FirstChild!;
        Assert.NotNull(hello);
        Assert.Equal("Hello", hello.Data!.Text);
        Assert.True(hello.Data.HasFeature(ExportedSegment.F_EOL_TAIL));

        // world
        TreeNode<ExportedSegment> world = hello.FirstChild!;
        Assert.NotNull(world);
        Assert.Empty(world.Children);
        Assert.Equal("world", world.Data!.Text);
        Assert.False(world.Data.HasFeature(ExportedSegment.F_EOL_TAIL));
    }

    [Fact]
    public void Apply_RootOnly_HandlesGracefully()
    {
        BlockLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> root = new();

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        Assert.Empty(result.Children);
        Assert.Null(result.Data);
    }

    [Fact]
    public void Apply_NewlineOnlyNode_Removed()
    {
        // que bixit|annos XX
        BlockLinearTextTreeFilter filter = new();

        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node;

        // que
        node = CreateTextNode("que");
        root.AddChild(node);
        TreeNode<ExportedSegment> current = node;

        // space
        node = CreateTextNode(" ");
        current.AddChild(node);
        current = node;

        // bixit
        node = CreateTextNode("bixit");
        current.AddChild(node);
        current = node;

        // LF
        node = CreateTextNode("\n");
        current.AddChild(node);
        current = node;

        // annos
        node = CreateTextNode("annos");
        current.AddChild(node);
        current = node;

        // space + XX
        node = CreateTextNode(" XX");
        current.AddChild(node);

        TreeNode<ExportedSegment> result = filter.Apply(root, new Item());

        // root
        Assert.Single(result.Children);
        // que
        TreeNode<ExportedSegment> que = result.Children[0];
        Assert.Equal("que", que.Data!.Text);
        // space
        Assert.Single(que.Children);
        TreeNode<ExportedSegment> space = que.Children[0];
        Assert.Equal(" ", space.Data!.Text);
        // bixit
        Assert.Single(space.Children);
        TreeNode<ExportedSegment> bixit = space.Children[0];
        Assert.Equal("bixit", bixit.Data!.Text);
        Assert.True(bixit.Data.HasFeature(ExportedSegment.F_EOL_TAIL));
        // annos
        Assert.Single(bixit.Children);
        TreeNode<ExportedSegment> annos = bixit.Children[0];
        Assert.Equal("annos", annos.Data!.Text);
        // space + XX
        Assert.Single(annos.Children);
        TreeNode<ExportedSegment> xx = annos.Children[0];
        Assert.Equal(" XX", xx.Data!.Text);
        Assert.False(xx.HasChildren);
    }
}