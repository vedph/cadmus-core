using System;
using System.Collections.Generic;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Graph.Adapters;
using Xunit;

namespace Cadmus.Graph.Extras.Test;

public sealed class ItemEidMetadataSourceTest
{
    [Fact]
    public void Supply_NullSource_Throws()
    {
        ItemEidMetadataSource source = new();
        MockCadmusRepository repository = new();

        Assert.Throws<ArgumentNullException>(() => source.Supply(
            null!, new Dictionary<string, object>(), repository));
    }

    [Fact]
    public void Supply_NullMetadata_Throws()
    {
        ItemEidMetadataSource source = new();
        Item item = new();
        GraphSource graphSource = new(item);
        MockCadmusRepository repository = new();

        Assert.Throws<ArgumentNullException>(
            () => source.Supply(graphSource, null!, repository));
    }

    [Fact]
    public void Supply_NullRepository_Throws()
    {
        ItemEidMetadataSource source = new();
        Item item = new();
        GraphSource graphSource = new(item);

        Assert.Throws<ArgumentNullException>(() => source.Supply(
            graphSource, new Dictionary<string, object>(), null));
    }

    [Fact]
    public void Supply_PartIsMetadataPartWithEid_SetsBothMetadata()
    {
        ItemEidMetadataSource source = new();
        Item item = new();
        MetadataPart part = new() { ItemId = item.Id };
        part.Metadata.Add(new Metadatum { Name = "eid", Value = "alpha" });
        GraphSource graphSource = new(item, part);
        MockCadmusRepository repository = new();
        Dictionary<string, object> metadata = [];

        source.Supply(graphSource, metadata, repository);

        Assert.Equal(part.Id, metadata[ItemEidMetadataSource.METADATA_PART_ID_KEY]);
        Assert.Equal("alpha", metadata[ItemEidMetadataSource.ITEM_EID_KEY]);
    }

    [Fact]
    public void Supply_PartIsMetadataPartWithoutEid_SetsOnlyPartId()
    {
        ItemEidMetadataSource source = new();
        Item item = new();
        MetadataPart part = new() { ItemId = item.Id };
        part.Metadata.Add(new Metadatum { Name = "other", Value = "x" });
        GraphSource graphSource = new(item, part);
        MockCadmusRepository repository = new();
        Dictionary<string, object> metadata = [];

        source.Supply(graphSource, metadata, repository);

        Assert.Equal(part.Id, metadata[ItemEidMetadataSource.METADATA_PART_ID_KEY]);
        Assert.False(metadata.ContainsKey(ItemEidMetadataSource.ITEM_EID_KEY));
    }

    [Fact]
    public void Supply_PartIsNotMetadataPart_FetchesFromRepository()
    {
        ItemEidMetadataSource source = new();
        Item item = new();
        // some other part type as the mapping's direct source
        NotePart otherPart = new() { ItemId = item.Id };
        GraphSource graphSource = new(item, otherPart);

        MetadataPart fetchedPart = new() { ItemId = item.Id };
        fetchedPart.Metadata.Add(new Metadatum
        {
            Name = "eid",
            Value = "beta"
        });
        MockCadmusRepository repository = new()
        {
            ItemParts = [fetchedPart]
        };
        Dictionary<string, object> metadata = [];

        source.Supply(graphSource, metadata, repository);

        Assert.Equal(fetchedPart.Id,
            metadata[ItemEidMetadataSource.METADATA_PART_ID_KEY]);
        Assert.Equal("beta", metadata[ItemEidMetadataSource.ITEM_EID_KEY]);
    }

    [Fact]
    public void Supply_NoMetadataPartFound_SetsNoMetadata()
    {
        ItemEidMetadataSource source = new();
        Item item = new();
        GraphSource graphSource = new(item);
        MockCadmusRepository repository = new(); // ItemParts empty
        Dictionary<string, object> metadata = [];

        source.Supply(graphSource, metadata, repository);

        Assert.Empty(metadata);
    }

    [Fact]
    public void AddItemEid_RegistersSourceInSupplier()
    {
        MetadataSupplier supplier = new();

        MetadataSupplier result = supplier.AddItemEid();

        Assert.Same(supplier, result);

        // verify it was actually wired in by exercising Supply()
        Item item = new();
        MetadataPart part = new() { ItemId = item.Id };
        part.Metadata.Add(new Metadatum { Name = "eid", Value = "gamma" });
        GraphSource graphSource = new(item, part);
        supplier.SetCadmusRepository(new MockCadmusRepository());
        Dictionary<string, object> metadata = [];

        supplier.Supply(graphSource, metadata);

        Assert.Equal("gamma", metadata[ItemEidMetadataSource.ITEM_EID_KEY]);
    }
}
