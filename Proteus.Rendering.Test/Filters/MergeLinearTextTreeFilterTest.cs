using Fusi.Tools;
using Fusi.Tools.Data;
using Proteus.Rendering.Filters;
using System.Collections.Generic;
using Xunit;

namespace Proteus.Rendering.Test.Filters;

public sealed class MergeLinearTextTreeFilterTest
{
    // Helper method to create a node with an ExportedSegment
    private static TreeNode<ExportedSegment> CreateNode(string text,
        IEnumerable<StringPair>? features = null,
        params object[] payloads)
    {
        return new TreeNode<ExportedSegment>(
            new ExportedSegment(text, features, payloads));
    }

    [Fact]
    public void Apply_WithEmptyTree_ReturnsEmptyTree()
    {
        MergeLinearTextTreeFilter filter = new();
        TreeNode<ExportedSegment> emptyRoot = new(new ExportedSegment(""));

        TreeNode<ExportedSegment> result = filter.Apply(emptyRoot);

        Assert.NotNull(result);
        Assert.Null(result.FirstChild);
    }

    [Fact]
    public void Apply_WithNoMergeableNodes_ReturnsSameStructure()
    {
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions
        {
            Features = ["type"]
        });

        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));
        TreeNode<ExportedSegment> nodeA = root.AddChild(CreateNode("A"));
        nodeA.Data!.AddFeature("type", "a");
        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(CreateNode("B"));
        nodeB.Data!.AddFeature("type", "b"); // different feature value

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);
        Assert.Equal("A", result.FirstChild.Data!.Text);
        Assert.NotNull(result.FirstChild.FirstChild);
        Assert.Equal("B", result.FirstChild.FirstChild.Data!.Text);
    }

    [Fact]
    public void Apply_WithMergeableNodes_MergesCorrectly()
    {
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions());

        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));
        TreeNode<ExportedSegment> nodeA = root.AddChild(CreateNode("A"));
        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(CreateNode("B"));
        TreeNode<ExportedSegment> nodeC = nodeB.AddChild(CreateNode("C"));

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);
        Assert.Equal("ABC", result.FirstChild.Data!.Text);
        // all nodes have been merged into one
        Assert.Null(result.FirstChild.FirstChild);
    }

    [Fact]
    public void Apply_WithSpecificFeatures_MergesNodesWithMatchingFeatures()
    {
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions
        {
            Features = ["style"]
        });

        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));

        // A with "style=italic" feature
        List<StringPair> featuresA = [new StringPair("style", "italic")];
        TreeNode<ExportedSegment> nodeA = root.AddChild(
            CreateNode("A", featuresA));

        // B with "style=italic" feature (will merge with A)
        List<StringPair> featuresB = [new StringPair("style", "italic")];
        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(
            CreateNode("B", featuresB));

        // C with "style=bold" feature (won't merge with A+B)
        List<StringPair> featuresC = [new StringPair("style", "bold")];
        TreeNode<ExportedSegment> nodeC = nodeB.AddChild(
            CreateNode("C", featuresC));

        // D with "style=bold" feature (will merge with C)
        List<StringPair> featuresD = [new StringPair("style", "bold")];
        TreeNode<ExportedSegment> nodeD = nodeC.AddChild(
            CreateNode("D", featuresD));

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);
        Assert.Equal("AB", result.FirstChild.Data!.Text);
        Assert.NotNull(result.FirstChild.FirstChild);
        Assert.Equal("CD", result.FirstChild.FirstChild.Data!.Text);
    }

    [Fact]
    public void Apply_WithNoBreakAtLF_RespectsLineFeedRule()
    {
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions
        {
            BreakAtLF = true
        });

        // root
        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));
        // A
        TreeNode<ExportedSegment> nodeA = root.AddChild(CreateNode("A"));
        // B+LF
        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(CreateNode("B\n"));
        // C
        TreeNode<ExportedSegment> nodeC = nodeB.AddChild(CreateNode("C"));

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);

        // A and B should merge
        Assert.Equal("AB\n", result.FirstChild.Data!.Text);
        Assert.NotNull(result.FirstChild.FirstChild);

        // C should not merge due to LF rule
        Assert.Equal("C", result.FirstChild.FirstChild.Data!.Text);
    }

    [Fact]
    public void Apply_WithValueFilters_AppliesFiltersToFeatureValues()
    {
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions
        {
            Features = ["id"],
            ValueFilters =
            [
                new() { Find = "-.*", IsRegex = true, Replace = "" }
            ]
        });

        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));

        // A with "id=seg-001" feature
        List<StringPair> featuresA = [new StringPair("id", "seg-001")];
        TreeNode<ExportedSegment> nodeA = root.AddChild(
            CreateNode("A", featuresA));

        // B with "id=seg-002" feature (will merge with A after filtering)
        List<StringPair> featuresB = [new StringPair("id", "seg-002")];
        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(
            CreateNode("B", featuresB));

        // C with "id=div-001" feature (won't merge, different prefix)
        List<StringPair> featuresC = [new StringPair("id", "div-001")];
        TreeNode<ExportedSegment> nodeC = nodeB.AddChild(
            CreateNode("C", featuresC));

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);
        // A and B merge after value filtering
        Assert.Equal("AB", result.FirstChild.Data!.Text);
        Assert.NotNull(result.FirstChild.FirstChild);
        // C doesn't merge due to different feature value
        Assert.Equal("C", result.FirstChild.FirstChild.Data!.Text);
    }

    [Fact]
    public void Apply_MergesLabelsCorrectly()
    {
        // Arrange
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions());

        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));
        TreeNode<ExportedSegment> nodeA = root.AddChild(CreateNode("A"));
        nodeA.Label = "Label1";

        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(CreateNode("B"));
        nodeB.Label = "Label2";

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);
        Assert.Equal("Label1Label2", result.FirstChild.Label);
        Assert.Equal("AB", result.FirstChild.Data!.Text);
    }

    [Fact]
    public void Apply_MergesFeaturesSetsAndPayloads()
    {
        MergeLinearTextTreeFilter filter = new();
        filter.Configure(new MergeLinearTextTreeFilterOptions
        {
            Features = [] // no features considered for merging criteria
        });

        TreeNode<ExportedSegment> root = new(new ExportedSegment(""));

        // A with feature, tag and payload
        TreeNode<ExportedSegment> nodeA = root.AddChild(
            CreateNode("A", [new StringPair("color", "red")], ["payload1"]));
        nodeA.Data!.Tags = ["tag1"];

        // B with different feature, tag and payload
        TreeNode<ExportedSegment> nodeB = nodeA.AddChild(
            CreateNode("B", [new StringPair("size", "large")], ["payload2"]));
        nodeB.Data!.Tags = ["tag2"];

        TreeNode<ExportedSegment> result = filter.Apply(root);

        Assert.NotNull(result);
        Assert.NotNull(result.FirstChild);
        Assert.Equal("AB", result.FirstChild.Data!.Text);

        // check merged features
        Assert.Equal(2, result.FirstChild.Data.Features!.Count);
        Assert.True(result.FirstChild.Data.HasFeature("color", "red"));
        Assert.True(result.FirstChild.Data.HasFeature("size", "large"));

        // check merged tags
        Assert.Equal(2, result.FirstChild.Data.Tags!.Count);
        Assert.Contains("tag1", result.FirstChild.Data.Tags);
        Assert.Contains("tag2", result.FirstChild.Data.Tags);

        // check merged payloads
        Assert.Equal(2, result.FirstChild.Data.Payloads!.Count);
        Assert.Contains("payload1", result.FirstChild.Data.Payloads);
        Assert.Contains("payload2", result.FirstChild.Data.Payloads);
    }
}
