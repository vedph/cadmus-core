using System.Collections.Generic;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class NodeMappingTest
{
    private static NodeMapping GetMapping()
    {
        return new NodeMapping
        {
            Id = 1,
            Ordinal = 2,
            Name = "sample",
            SourceType = Node.SOURCE_ITEM,
            FacetFilter = "person",
            GroupFilter = "alpha.*",
            FlagsFilter = 0x10,
            TitleFilter = "title",
            PartTypeFilter = "it.vedph.metadata",
            PartRoleFilter = "role",
            Description = "A sample mapping",
            Source = "events",
            Sid = "{$item-id}"
        };
    }

    [Fact]
    public void IsScalarMatch_NoPattern_AlwaysTrue()
    {
        NodeMapping mapping = new();

        Assert.True(mapping.IsScalarMatch(null));
        Assert.True(mapping.IsScalarMatch("anything"));
    }

    [Fact]
    public void IsScalarMatch_WithPattern_NullValue_False()
    {
        NodeMapping mapping = new() { ScalarPattern = "^true$" };

        Assert.False(mapping.IsScalarMatch(null));
    }

    [Fact]
    public void IsScalarMatch_WithPattern_Matching_True()
    {
        NodeMapping mapping = new() { ScalarPattern = "^true$" };

        Assert.True(mapping.IsScalarMatch("true"));
        Assert.False(mapping.IsScalarMatch("false"));
    }

    [Fact]
    public void HasChildren_NoChildren_False()
    {
        NodeMapping mapping = new();

        Assert.False(mapping.HasChildren);
    }

    [Fact]
    public void HasChildren_WithChildren_True()
    {
        NodeMapping mapping = new();
        mapping.Children.Add(new NodeMapping { Name = "child" });

        Assert.True(mapping.HasChildren);
    }

    [Fact]
    public void Visit_VisitsAllDescendants()
    {
        NodeMapping root = new() { Name = "root" };
        NodeMapping childA = new() { Name = "a" };
        NodeMapping childB = new() { Name = "b" };
        NodeMapping grandChild = new() { Name = "a1" };
        childA.Children.Add(grandChild);
        root.Children.Add(childA);
        root.Children.Add(childB);

        List<string?> names = [];
        root.Visit(m =>
        {
            names.Add(m.Name);
            return true;
        });

        Assert.Equal(["root", "a", "a1", "b"], names);
    }

    [Fact]
    public void Visit_StopsWhenVisitorReturnsFalse()
    {
        NodeMapping root = new() { Name = "root" };
        NodeMapping child = new() { Name = "child" };
        root.Children.Add(child);

        List<string?> names = [];
        root.Visit(m =>
        {
            names.Add(m.Name);
            return false;
        });

        Assert.Equal(["root"], names);
    }

    [Fact]
    public void Clone_DeepCopiesMappingAndChildren()
    {
        NodeMapping mapping = GetMapping();
        mapping.Children.Add(new NodeMapping { Name = "child" });

        NodeMapping clone = mapping.Clone();

        Assert.NotSame(mapping, clone);
        Assert.Equal(mapping.Id, clone.Id);
        Assert.Equal(mapping.Name, clone.Name);
        Assert.Equal(mapping.FacetFilter, clone.FacetFilter);
        Assert.Equal(mapping.GroupFilter, clone.GroupFilter);
        Assert.Equal(mapping.FlagsFilter, clone.FlagsFilter);
        Assert.Equal(mapping.TitleFilter, clone.TitleFilter);
        Assert.Equal(mapping.PartTypeFilter, clone.PartTypeFilter);
        Assert.Equal(mapping.PartRoleFilter, clone.PartRoleFilter);
        Assert.Equal(mapping.Description, clone.Description);
        Assert.Equal(mapping.Source, clone.Source);
        Assert.Equal(mapping.Sid, clone.Sid);
        Assert.Single(clone.Children);
        Assert.NotSame(mapping.Children[0], clone.Children[0]);
        Assert.Equal("child", clone.Children[0].Name);
    }

    [Fact]
    public void ToString_IncludesFiltersAndSource()
    {
        NodeMapping mapping = GetMapping();

        string s = mapping.ToString();

        Assert.Contains("#1 sample", s);
        Assert.Contains("facet=person", s);
        Assert.Contains("group=alpha.*", s);
        Assert.Contains("title=title", s);
        Assert.Contains("type=it.vedph.metadata", s);
        Assert.Contains("role=role", s);
        Assert.Contains(": events", s);
    }

    [Fact]
    public void ToString_NoFilters_OmitsBrackets()
    {
        NodeMapping mapping = new()
        {
            Id = 5,
            Name = "plain",
            Source = "source-expr"
        };

        string s = mapping.ToString();

        Assert.DoesNotContain("[", s);
        Assert.Contains("#5 plain", s);
        Assert.Contains(": source-expr", s);
    }
}
