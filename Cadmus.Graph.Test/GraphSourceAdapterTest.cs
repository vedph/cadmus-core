using System;
using System.Collections.Generic;
using Cadmus.Core;
using Cadmus.Graph.Adapters;
using Cadmus.General.Parts;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class GraphSourceTest
{
    [Fact]
    public void Ctor_ItemOnly_NullItem_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GraphSource((IItem)null!));
    }

    [Fact]
    public void Ctor_ItemOnly_Ok()
    {
        Item item = new();

        GraphSource source = new(item);

        Assert.Same(item, source.Item);
        Assert.Null(source.Part);
    }

    [Fact]
    public void Ctor_ItemAndPart_NullItem_Throws()
    {
        MetadataPart part = new();

        Assert.Throws<ArgumentNullException>(
            () => new GraphSource(null!, part));
    }

    [Fact]
    public void Ctor_ItemAndPart_NullPart_Throws()
    {
        Item item = new();

        Assert.Throws<ArgumentNullException>(
            () => new GraphSource(item, null!));
    }

    [Fact]
    public void Ctor_ItemAndPart_Ok()
    {
        Item item = new();
        MetadataPart part = new();

        GraphSource source = new(item, part);

        Assert.Same(item, source.Item);
        Assert.Same(part, source.Part);
    }

    [Fact]
    public void ToString_NoPart_ReturnsItemString()
    {
        Item item = new() { Title = "Sample" };

        GraphSource source = new(item);

        Assert.Equal(item.ToString(), source.ToString());
    }

    [Fact]
    public void ToString_WithPart_ReturnsPartString()
    {
        Item item = new();
        MetadataPart part = new();

        GraphSource source = new(item, part);

        Assert.Equal(part.ToString(), source.ToString());
    }
}

public sealed class ItemGraphSourceAdapterTest
{
    private static Item GetItem(string? groupId = null)
    {
        return new Item
        {
            Title = "Sample item",
            FacetId = "person",
            GroupId = groupId,
            Flags = 3
        };
    }

    [Fact]
    public void Adapt_SimpleItem_SetsFilterAndMetadata()
    {
        ItemGraphSourceAdapter adapter = new();
        Item item = GetItem();
        GraphSource source = new(item);
        Dictionary<string, object> metadata = [];

        (object? result, RunNodeMappingFilter filter) =
            adapter.Adapt(source, metadata);

        Assert.NotNull(result);
        Assert.Equal(Node.SOURCE_ITEM, filter.SourceType);
        Assert.Equal("person", filter.Facet);
        Assert.Equal(item.Title, filter.Title);
        Assert.Equal(item.Id, metadata[ItemGraphSourceAdapter.M_ITEM_ID]);
        Assert.Equal(item.Title, metadata[ItemGraphSourceAdapter.M_ITEM_TITLE]);
        Assert.Equal("person", metadata[ItemGraphSourceAdapter.M_ITEM_FACET]);
        Assert.False(metadata.ContainsKey(ItemGraphSourceAdapter.M_ITEM_GROUP));
    }

    [Fact]
    public void Adapt_SimpleGroup_SetsGroupMetadatumOnly()
    {
        ItemGraphSourceAdapter adapter = new();
        Item item = GetItem("alpha");
        GraphSource source = new(item);
        Dictionary<string, object> metadata = [];

        adapter.Adapt(source, metadata);

        Assert.Equal("alpha", metadata[ItemGraphSourceAdapter.M_ITEM_GROUP]);
        Assert.False(metadata.ContainsKey(
            $"{ItemGraphSourceAdapter.M_ITEM_GROUP}@1"));
    }

    [Fact]
    public void Adapt_CompositeGroup_SplitsIntoComponents()
    {
        ItemGraphSourceAdapter adapter = new();
        Item item = GetItem("alpha/beta");
        GraphSource source = new(item);
        Dictionary<string, object> metadata = [];

        adapter.Adapt(source, metadata);

        Assert.Equal("alpha/beta",
            metadata[ItemGraphSourceAdapter.M_ITEM_GROUP]);
        Assert.Equal("alpha",
            metadata[$"{ItemGraphSourceAdapter.M_ITEM_GROUP}@1"]);
        Assert.Equal("beta",
            metadata[$"{ItemGraphSourceAdapter.M_ITEM_GROUP}@2"]);
    }
}

public sealed class PartGraphSourceAdapterTest
{
    [Fact]
    public void Adapt_NoPart_ReturnsNull()
    {
        PartGraphSourceAdapter adapter = new();
        Item item = new();
        GraphSource source = new(item);
        Dictionary<string, object> metadata = [];

        (object? result, _) = adapter.Adapt(source, metadata);

        Assert.Equal("null", result);
    }

    [Fact]
    public void Adapt_WithPart_SetsFilterAndMetadata()
    {
        PartGraphSourceAdapter adapter = new();
        Item item = new() { FacetId = "person" };
        MetadataPart part = new()
        {
            ItemId = item.Id,
            RoleId = "role1"
        };
        GraphSource source = new(item, part);
        Dictionary<string, object> metadata = [];

        (object? result, RunNodeMappingFilter filter) =
            adapter.Adapt(source, metadata);

        Assert.NotNull(result);
        Assert.Equal(Node.SOURCE_PART, filter.SourceType);
        Assert.Equal(part.TypeId, filter.PartType);
        Assert.Equal("role1", filter.PartRole);
        Assert.Equal(part.Id, metadata[PartGraphSourceAdapter.M_PART_ID]);
        Assert.Equal(part.ItemId,
            metadata[ItemGraphSourceAdapter.M_ITEM_ID]);
        Assert.Equal(part.TypeId,
            metadata[PartGraphSourceAdapter.M_PART_TYPE_ID]);
        Assert.Equal("role1",
            metadata[PartGraphSourceAdapter.M_PART_ROLE_ID]);
    }

    [Fact]
    public void Adapt_WithPart_NoRole_OmitsRoleMetadatum()
    {
        PartGraphSourceAdapter adapter = new();
        Item item = new();
        MetadataPart part = new() { ItemId = item.Id };
        GraphSource source = new(item, part);
        Dictionary<string, object> metadata = [];

        adapter.Adapt(source, metadata);

        Assert.False(metadata.ContainsKey(
            PartGraphSourceAdapter.M_PART_ROLE_ID));
    }
}
