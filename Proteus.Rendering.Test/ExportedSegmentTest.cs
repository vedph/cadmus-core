namespace Proteus.Rendering.Test;

public sealed class ExportedSegmentTest
{
    [Fact]
    public void AddFeature_BasicAdd_AddsFeature()
    {
        ExportedSegment segment = new();

        segment.AddFeature("color", "red");

        Assert.NotNull(segment.Features);
        Assert.Single(segment.Features);
        Assert.Equal("color", segment.Features[0].Name);
        Assert.Equal("red", segment.Features[0].Value);
    }

    [Fact]
    public void AddFeature_DuplicateNameValue_IgnoresDuplicate()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");

        segment.AddFeature("color", "red");

        Assert.Single(segment.Features!);
    }

    [Fact]
    public void AddFeature_Unique_ReplacesExistingFeature()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");

        segment.AddFeature("color", "blue", true);

        Assert.NotNull(segment.Features);
        Assert.Single(segment.Features);
        Assert.Equal("color", segment.Features[0].Name);
        Assert.Equal("blue", segment.Features[0].Value);
    }

    [Fact]
    public void AddFeature_NotUnique_AddsMultipleWithSameName()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");

        segment.AddFeature("color", "blue");

        Assert.Equal(2, segment.Features!.Count);
        Assert.Equal("red", segment.Features[0].Value);
        Assert.Equal("blue", segment.Features[1].Value);
    }

    [Fact]
    public void RemoveFeatures_NullName_RemovesAllFeatures()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");
        segment.AddFeature("size", "large");

        segment.RemoveFeatures();

        Assert.Null(segment.Features);
    }

    [Fact]
    public void RemoveFeatures_WithName_RemovesMatchingFeatures()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");
        segment.AddFeature("color", "blue");
        segment.AddFeature("size", "large");

        segment.RemoveFeatures("color");

        Assert.NotNull(segment.Features);
        Assert.Single(segment.Features);
        Assert.Equal("size", segment.Features[0].Name);
    }

    [Fact]
    public void RemoveFeatures_WithNameAndValue_RemovesSpecificFeature()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");
        segment.AddFeature("color", "blue");

        segment.RemoveFeatures("color", "red");

        Assert.NotNull(segment.Features);
        Assert.Single(segment.Features);
        Assert.Equal("blue", segment.Features[0].Value);
    }

    [Fact]
    public void HasFeature_ExistingFeature_ReturnsTrue()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");

        Assert.True(segment.HasFeature("color"));
        Assert.True(segment.HasFeature("color", "red"));
    }

    [Fact]
    public void HasFeature_NonExistingFeature_ReturnsFalse()
    {
        ExportedSegment segment = new();
        segment.AddFeature("color", "red");

        Assert.False(segment.HasFeature("size"));
        Assert.False(segment.HasFeature("color", "blue"));
    }

    [Fact]
    public void Clone_CopiesAllProperties()
    {
        ExportedSegment segment = new()
        {
            SourceId = 42,
            Type = "paragraph",
            Text = "Sample text",
            Tags = ["tag1", "tag2"]
        };
        segment.AddFeature("color", "red");
        segment.Payloads = ["payload1", 123];

        ExportedSegment clone = segment.Clone();

        Assert.Equal(segment.SourceId, clone.SourceId);
        Assert.Equal(segment.Type, clone.Type);
        Assert.Equal(segment.Text, clone.Text);
        Assert.Equal(segment.Features!.Count, clone.Features!.Count);
        Assert.Equal(segment.Tags!.Count, clone.Tags!.Count);
        Assert.Equal(segment.Payloads!.Count, clone.Payloads!.Count);
        Assert.True(segment.Tags.SetEquals(clone.Tags));
    }

    [Fact]
    public void ToString_ReturnsFormattedString()
    {
        ExportedSegment segment = new()
        {
            SourceId = 42,
            Text = "Sample text",
            Tags = ["tag1", "tag2"]
        };
        segment.AddFeature("color", "red");

        string result = segment.ToString();

        Assert.Contains("#42", result);
        Assert.Contains("Sample text", result);
        Assert.Contains("color=red", result);
        Assert.Contains("[tag1,tag2]", result);
    }

    [Fact]
    public void MergeSegments_BothNull_ReturnsNull()
    {
        ExportedSegment? result = ExportedSegment.MergeSegments(null, null);

        Assert.Null(result);
    }

    [Fact]
    public void MergeSegments_SourceNull_ReturnsTarget()
    {
        ExportedSegment target = new() { Text = "Target" };

        ExportedSegment? result = ExportedSegment.MergeSegments(null, target);

        Assert.Same(target, result);
    }

    [Fact]
    public void MergeSegments_TargetNull_ReturnsSource()
    {
        ExportedSegment source = new() { Text = "Source" };

        ExportedSegment? result = ExportedSegment.MergeSegments(source, null);

        Assert.Same(source, result);
    }

    [Fact]
    public void MergeSegments_MergesAllProperties()
    {
        ExportedSegment source = new()
        {
            Text = "Source",
            Tags = ["sourceTag"],
            Payloads = ["sourcePayload"]
        };
        source.AddFeature("sourceFeature", "value");

        ExportedSegment target = new()
        {
            Text = "Target",
            Tags = ["targetTag"],
            Payloads = ["targetPayload"]
        };
        target.AddFeature("targetFeature", "value");

        ExportedSegment? result = ExportedSegment.MergeSegments(source, target);

        Assert.Same(target, result);
        Assert.Equal("TargetSource", result!.Text);
        Assert.Equal(2, result.Features!.Count);
        Assert.Contains("sourceTag", result.Tags!);
        Assert.Contains("targetTag", result.Tags!);
        Assert.Equal(2, result.Payloads!.Count);
    }
}
