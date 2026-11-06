using Cadmus.Core;
using Cadmus.Export.Mapping;
using Cadmus.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Graph.Test;

/// <summary>
/// Tests for the default members of <see cref="IGraphRepository"/>, the
/// updater's macros and explanations, and the mapper's node references.
/// </summary>
public sealed class GraphCoverageTest
{
    #region IGraphRepository default members
    [Fact]
    public async Task DefaultAsyncMembers_WrapSyncMembers()
    {
        MockGraphRepository mock = new();
        IGraphRepository repository = mock;
        GraphSet set = new();

        await repository.UpdateGraphAsync(set);
        await repository.DeleteGraphSetAsync("x");
        GraphSet got = await repository.GetGraphSetAsync("y");
        IList<GraphNodeMapping> mappings = await repository.FindMappingsAsync(
            new RunNodeMappingFilter { SourceType = Node.SOURCE_ITEM });

        Assert.Same(set, Assert.Single(mock.UpdatedSets));
        Assert.Equal("x", Assert.Single(mock.DeletedSourceIds));
        Assert.Contains("y", got.SourceIds);
        Assert.Empty(mappings);
    }

    [Fact]
    public async Task DefaultAsyncMembers_Cancelled_Throw()
    {
        IGraphRepository repository = new MockGraphRepository();
        CancellationToken cancelled = new(true);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.UpdateGraphAsync(new GraphSet(), cancelled));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.DeleteGraphSetAsync("x", cancelled));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.GetGraphSetAsync("x", cancelled));
        await Assert.ThrowsAsync<OperationCanceledException>(
            () => repository.FindMappingsAsync(new RunNodeMappingFilter(),
                cancelled));
    }
    #endregion

    #region GraphUpdater
    private sealed class EchoMacro : INodeMappingMacro
    {
        public string? Run(MapperSource? context, string[]? args) =>
            args == null ? "none" : string.Join("-", args);
    }

    private static GraphNodeMapping GetItemMapping(string uid) => new()
    {
        Name = "item",
        SourceType = Node.SOURCE_ITEM,
        Source = ".",
        Sid = "{$item-id}",
        Output = new NodeMappingOutput
        {
            Nodes = new Dictionary<string, MappedNode>
            {
                ["item"] = new MappedNode { Uid = uid }
            }
        }
    };

    private static Item GetItem() => new()
    {
        Title = "Petrarch",
        FacetId = "person",
        UserId = "zeus",
        CreatorId = "zeus"
    };

    [Fact]
    public void Ctor_NullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new GraphUpdater(null!));
    }

    [Fact]
    public void Update_NullArgs_Throw()
    {
        GraphUpdater updater = new(new MockGraphRepository());

        Assert.Throws<ArgumentNullException>(() => updater.Update(null!));
        Assert.Throws<ArgumentNullException>(
            () => updater.Update(GetItem(), null!));
        Assert.Throws<ArgumentNullException>(() => updater.Explain(null!));
        Assert.Throws<ArgumentNullException>(
            () => updater.Explain(GetItem(), null!));
    }

    [Fact]
    public void Macros_AddDeleteSetReset_Ok()
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetItemMapping("x:items/{!echo(a & b)}"));
        GraphUpdater updater = new(repository);

        updater.AddMacro("echo", new EchoMacro());
        updater.Update(GetItem());
        Assert.Equal("x:items/a-b",
            Assert.Single(repository.UpdatedSets[^1].Nodes).Uri);

        // deleted macro: resolved as empty
        updater.DeleteMacro("echo");
        updater.Update(GetItem());
        Assert.Equal("x:items/",
            Assert.Single(repository.UpdatedSets[^1].Nodes).Uri);

        updater.SetMacros(new Dictionary<string, INodeMappingMacro>
        {
            ["echo"] = new EchoMacro()
        });
        updater.Update(GetItem());
        Assert.Equal("x:items/a-b",
            Assert.Single(repository.UpdatedSets[^1].Nodes).Uri);

        updater.ResetMacros();
        updater.Update(GetItem());
        Assert.Equal("x:items/",
            Assert.Single(repository.UpdatedSets[^1].Nodes).Uri);
    }

    [Fact]
    public void Explain_Item_Ok()
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetItemMapping("x:items/{$item-title}"));
        GraphUpdater updater = new(repository);
        updater.Metadata["extra"] = "value";
        Item item = GetItem();

        GraphUpdaterExplanation? explanation = updater.Explain(item);

        Assert.NotNull(explanation);
        Assert.Equal(Node.SOURCE_ITEM, explanation.Filter!.SourceType);
        Assert.Equal("x:items/petrarch",
            Assert.Single(explanation.Set.Nodes).Uri);
        Assert.Equal("item", Assert.Single(explanation.Mappings).Name);
        Assert.Equal(item.Id, explanation.Metadata["item-id"]);
        Assert.Equal("value", explanation.Metadata["extra"]);
        Assert.Empty(updater.Metadata);
    }

    [Fact]
    public void Update_MetadataSupplier_Supplied()
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetItemMapping("x:items/{$custom}"));
        GraphUpdater updater = new(repository)
        {
            MetadataSupplier = new MetadataSupplier()
                .AddMetadataSource(new CustomMetadataSource())
        };

        updater.Update(GetItem());

        Assert.Equal("x:items/supplied",
            Assert.Single(repository.UpdatedSets[^1].Nodes).Uri);
    }

    private sealed class CustomMetadataSource : IMetadataSource
    {
        public void Supply(MapperSource source,
            IDictionary<string, object> metadata,
            Core.Storage.ICadmusRepository? repository, object? context = null)
        {
            metadata["custom"] = "supplied";
        }
    }
    #endregion

    #region JsonGraphNodeMapper
    private static GraphSet Map(GraphNodeMapping mapping, string json)
    {
        JsonGraphNodeMapper mapper = new();
        GraphSet set = new();
        mapper.Map(json, mapping, set);
        return set;
    }

    [Fact]
    public void Map_NodeKeySuffixes_Ok()
    {
        GraphNodeMapping mapping = new()
        {
            Name = "m",
            SourceType = Node.SOURCE_ITEM,
            Source = "name",
            Sid = "sid-1",
            Output = new NodeMappingOutput
            {
                Nodes = new Dictionary<string, MappedNode>
                {
                    ["n"] = new MappedNode { Uid = "x:{$.}", Label = "L" }
                },
                Triples =
                [
                    new MappedTriple
                    {
                        S = "{?n}",
                        P = "x:p",
                        OL = "\"{?n:label}|{?n:sid}|{?n:src_type}|{?n:uri}|{?zz}\""
                    }
                ]
            }
        };

        GraphSet set = Map(mapping, "{\"name\":\"john\"}");

        UriTriple triple = Assert.Single(set.Triples);
        Assert.Equal("x:john", triple.SubjectUri);
        Assert.Equal("L|sid-1|1|x:john|", triple.ObjectLiteral);
    }

    [Theory]
    [InlineData(null, "x:p", "x:o")]
    [InlineData("x:s", null, "x:o")]
    [InlineData("x:s", "x:p", null)]
    public void Map_IncompleteTriple_Throws(string? s, string? p, string? o)
    {
        GraphNodeMapping mapping = new()
        {
            Name = "m",
            SourceType = Node.SOURCE_ITEM,
            Source = ".",
            Sid = "sid-1",
            Output = new NodeMappingOutput
            {
                Triples = [new MappedTriple { S = s, P = p, O = o }]
            }
        };

        Assert.Throws<CadmusGraphException>(() => Map(mapping, "{}"));
    }

    [Fact]
    public void Map_NoSid_Throws()
    {
        GraphNodeMapping mapping = new()
        {
            Name = "m",
            SourceType = Node.SOURCE_ITEM,
            Source = ".",
            Output = new NodeMappingOutput
            {
                Nodes = new Dictionary<string, MappedNode>
                {
                    ["n"] = new MappedNode { Uid = "x:n" }
                }
            }
        };

        Assert.Throws<CadmusGraphException>(() => Map(mapping, "{}"));
    }

    [Fact]
    public void Map_NoSidMetadataOnly_Ok()
    {
        GraphNodeMapping mapping = new()
        {
            Name = "m",
            SourceType = Node.SOURCE_ITEM,
            Source = ".",
            Output = new NodeMappingOutput
            {
                Metadata = new Dictionary<string, string> { ["k"] = "v" }
            }
        };

        GraphSet set = Map(mapping, "{}");

        Assert.Empty(set.Nodes);
        Assert.Empty(set.Triples);
    }
    #endregion

    #region CrudGrouper
    [Fact]
    public void CrudGrouper_GroupAndFilterDeleted_Ok()
    {
        CrudGrouper<int> grouper = new();

        grouper.Group([1, 2, 3], [2, 3, 4, 5], (a, b) => a == b);
        grouper.FilterDeleted(n => n % 2 == 0);

        Assert.Equal([1], grouper.Added);
        Assert.Equal([2, 3], grouper.Updated);
        Assert.Equal([4], grouper.Deleted);
        Assert.Throws<ArgumentNullException>(() => grouper.FilterDeleted(null!));
    }
    #endregion
}
