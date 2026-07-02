/*
using System;
using Xunit;

namespace Cadmus.Export.Test;

public sealed class FragmentFeatureSourceTest
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        const string typeId = "type1";
        const string roleId = "role1";
        const int index = 1;
        const string suffix = "suffix1";

        FragmentFeatureSource source = new(typeId, roleId, index, suffix);

        Assert.Equal(typeId, source.TypeId);
        Assert.Equal(roleId, source.RoleId);
        Assert.Equal(index, source.Index);
        Assert.Equal(suffix, source.Suffix);
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormat()
    {
        FragmentFeatureSource source = new("type1", "role1", 1, "suffix1");

        string result = source.ToString();

        Assert.Equal("type1:role1@1suffix1", result);
    }

    [Fact]
    public void ToString_ShouldReturnCorrectFormatWithoutSuffix()
    {
        FragmentFeatureSource source = new("type1", "role1", 1);

        string result = source.ToString();

        Assert.Equal("type1:role1@1", result);
    }

    [Fact]
    public void Parse_ShouldReturnCorrectObject()
    {
        const string text = "type1:role1@1suffix1";

        FragmentFeatureSource result = FragmentFeatureSource.Parse(text);

        Assert.Equal("type1", result.TypeId);
        Assert.Equal("role1", result.RoleId);
        Assert.Equal(1, result.Index);
        Assert.Equal("suffix1", result.Suffix);
    }

    [Fact]
    public void Parse_ShouldReturnCorrectObjectWithoutSuffix()
    {
        const string text = "type1:role1@1";

        FragmentFeatureSource result = FragmentFeatureSource.Parse(text);

        Assert.Equal("type1", result.TypeId);
        Assert.Equal("role1", result.RoleId);
        Assert.Equal(1, result.Index);
        Assert.Null(result.Suffix);
    }

    [Fact]
    public void Parse_ShouldThrowFormatExceptionForInvalidFormat()
    {
        const string text = "invalid_format";

        Assert.Throws<FormatException>(() => FragmentFeatureSource.Parse(text));
    }

    [Fact]
    public void Parse_ShouldThrowFormatExceptionForInvalidIndex()
    {
        const string text = "type1:role1@invalid_index";

        Assert.Throws<FormatException>(() => FragmentFeatureSource.Parse(text));
    }

    [Fact]
    public void Parse_ShouldThrowArgumentNullExceptionForNullText()
    {
        Assert.Throws<ArgumentNullException>(() =>
        FragmentFeatureSource.Parse(null!));
    }
}
*/