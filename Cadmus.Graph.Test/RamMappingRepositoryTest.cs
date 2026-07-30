using System;
using System.IO;
using Fusi.Tools.Data;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class RamMappingRepositoryTest
{
    private static GraphNodeMapping GetMapping(string name, int sourceType,
        string? facet = null, string? group = null, string? title = null,
        string? partType = null, string? partRole = null, int? flags = null)
    {
        return new GraphNodeMapping
        {
            Name = name,
            SourceType = sourceType,
            FacetFilter = facet,
            GroupFilter = group,
            TitleFilter = title,
            PartTypeFilter = partType,
            PartRoleFilter = partRole,
            FlagsFilter = flags,
            Source = "source"
        };
    }

    [Fact]
    public void AddMapping_New_GetsId()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping mapping = GetMapping("m1", Node.SOURCE_ITEM);

        int id = repository.AddMapping(mapping);

        Assert.True(id > 0);
        Assert.Equal(id, mapping.Id);
        Assert.NotNull(repository.GetMapping(id));
    }

    [Fact]
    public void AddMapping_ExistingId_Replaces()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping mapping = GetMapping("m1", Node.SOURCE_ITEM);
        int id = repository.AddMapping(mapping);

        GraphNodeMapping updated = GetMapping("m1-updated", Node.SOURCE_ITEM);
        updated.Id = id;
        repository.AddMapping(updated);

        GraphNodeMapping? fetched = repository.GetMapping(id);
        Assert.NotNull(fetched);
        Assert.Equal("m1-updated", fetched!.Name);
        Assert.Single(repository.Mappings);
    }

    [Fact]
    public void AddMappingByName_Null_Throws()
    {
        RamMappingRepository repository = new();

        Assert.Throws<ArgumentNullException>(
            () => repository.AddMappingByName(null!));
    }

    [Fact]
    public void AddMappingByName_NonZeroId_Throws()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping mapping = GetMapping("m1", Node.SOURCE_ITEM);
        mapping.Id = 42;

        Assert.Throws<InvalidOperationException>(
            () => repository.AddMappingByName(mapping));
    }

    [Fact]
    public void AddMappingByName_New_AddsMapping()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping mapping = GetMapping("m1", Node.SOURCE_ITEM);

        int id = repository.AddMappingByName(mapping);

        Assert.True(id > 0);
        Assert.NotNull(repository.GetMapping(id));
    }

    [Fact]
    public void AddMappingByName_ExistingName_ReplacesCaseInsensitive()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping mapping = GetMapping("M1", Node.SOURCE_ITEM);
        int id = repository.AddMappingByName(mapping);

        GraphNodeMapping updated = GetMapping("m1", Node.SOURCE_ITEM,
            facet: "person");
        repository.AddMappingByName(updated);

        Assert.Single(repository.Mappings);
        Assert.Equal("person", repository.Mappings[0].FacetFilter);
    }

    [Fact]
    public void DeleteMapping_Existing_Removes()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping mapping = GetMapping("m1", Node.SOURCE_ITEM);
        int id = repository.AddMapping(mapping);

        repository.DeleteMapping(id);

        Assert.Null(repository.GetMapping(id));
    }

    [Fact]
    public void DeleteMapping_NotExisting_Nope()
    {
        RamMappingRepository repository = new();
        repository.AddMapping(GetMapping("m1", Node.SOURCE_ITEM));

        repository.DeleteMapping(999);

        Assert.Single(repository.Mappings);
    }

    [Fact]
    public void GetMappings_FiltersByParentSourceNameFacetGroupFlagsTitlePartTypeRole()
    {
        RamMappingRepository repository = new();
        GraphNodeMapping root = GetMapping("person-mapping", Node.SOURCE_ITEM,
            facet: "person", group: "alpha.*", title: "Some Title",
            partType: "it.vedph.metadata", partRole: "role1", flags: 0x03);
        int rootId = repository.AddMapping(root);
        GraphNodeMapping child = GetMapping("child", Node.SOURCE_PART);
        child.ParentId = rootId;
        repository.AddMapping(child);

        Assert.Equal(2, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            ParentId = rootId
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            SourceType = Node.SOURCE_ITEM
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            Name = "person"
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            Facet = "person"
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            Group = "alpha"
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            Flags = 0x01
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            Title = "Title"
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            PartType = "it.vedph.metadata"
        }, false).Total);

        Assert.Equal(1, repository.GetMappings(new NodeMappingFilter
        {
            PageSize = 10,
            PartRole = "role1"
        }, false).Total);
    }

    [Fact]
    public void GetMappings_EmptyResult_Ok()
    {
        RamMappingRepository repository = new();

        DataPage<GraphNodeMapping> page = repository.GetMappings(
            new NodeMappingFilter { PageSize = 10 }, false);

        Assert.Equal(0, page.Total);
        Assert.Empty(page.Items);
    }

    [Fact]
    public void GetMappings_PageSizeZero_ReturnsAll()
    {
        RamMappingRepository repository = new();
        for (int i = 1; i <= 3; i++)
            repository.AddMapping(GetMapping($"m{i}", Node.SOURCE_ITEM));

        DataPage<GraphNodeMapping> page = repository.GetMappings(
            new NodeMappingFilter { PageSize = 0 }, false);

        Assert.Equal(3, page.Items.Count);
    }

    [Fact]
    public void Import_Null_Throws()
    {
        RamMappingRepository repository = new();

        Assert.Throws<ArgumentNullException>(() => repository.Import(null!));
    }

    [Fact]
    public void Import_InvalidJson_Throws()
    {
        RamMappingRepository repository = new();

        Assert.Throws<InvalidDataException>(() => repository.Import("null"));
    }

    [Fact]
    public void ImportExport_RoundTrip_Ok()
    {
        RamMappingRepository repository = new();
        repository.AddMapping(GetMapping("m1", Node.SOURCE_ITEM,
            facet: "person"));

        string json = repository.Export();

        RamMappingRepository repository2 = new();
        int n = repository2.Import(json);

        Assert.Equal(1, n);
        Assert.Single(repository2.Mappings);
        Assert.Equal("m1", repository2.Mappings[0].Name);
    }
}
