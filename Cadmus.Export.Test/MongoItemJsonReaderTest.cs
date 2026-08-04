using Cadmus.Export.Mapping;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Export.Test;

[Collection(nameof(NonParallelResourceCollection))]
public sealed class MongoItemJsonReaderTest(MongoFixture fixture) :
    IClassFixture<MongoFixture>
{
    private readonly MongoFixture _fixture = fixture;

    private static MongoItemJsonReader GetReader()
    {
        MongoItemJsonReader reader = new();
        reader.Configure(new MongoItemJsonReaderOptions
        {
            ConnectionString = "mongodb://localhost:27017/test-db"
        });
        return reader;
    }

    [Fact]
    public async Task ReadAsync_NullItemId_ThrowsArgumentNullException()
    {
        using MongoItemJsonReader reader = GetReader();

        await Assert.ThrowsAsync<ArgumentNullException>(
            () => reader.ReadAsync(null!, null));
    }

    [Fact]
    public async Task ReadAsync_NotConfigured_ThrowsInvalidOperationException()
    {
        using MongoItemJsonReader reader = new();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => reader.ReadAsync("item1", null));
    }

    [Fact]
    public async Task ReadAsync_ItemNotFound_ReturnsNull()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        JsonDocument? doc = await reader.ReadAsync("not-existing", null);

        Assert.Null(doc);
    }

    [Fact]
    public async Task ReadAsync_ItemFound_NoFilter_ReturnsItemWithAllParts()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        using JsonDocument? doc = await reader.ReadAsync("item2", null);

        Assert.NotNull(doc);
        JsonElement root = doc.RootElement;
        Assert.Equal("item2", root.GetProperty("_id").GetString());
        Assert.Equal("Item 2", root.GetProperty("title").GetString());

        JsonElement parts = root.GetProperty("parts");
        Assert.Equal(JsonValueKind.Array, parts.ValueKind);
        Assert.Equal(2, parts.GetArrayLength());
    }

    [Fact]
    public async Task ReadAsync_ItemWithoutFilter_PartsHaveTypeAndRole()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        using JsonDocument? doc = await reader.ReadAsync("item2", null);

        Assert.NotNull(doc);
        JsonElement parts = doc.RootElement.GetProperty("parts");

        bool foundComment = false, foundToken = false;
        foreach (JsonElement part in parts.EnumerateArray())
        {
            string typeId = part.GetProperty("typeId").GetString()!;
            if (typeId == "comment")
            {
                foundComment = true;
                Assert.Equal("content2",
                    part.GetProperty("content").GetProperty("value")
                        .GetString());
            }
            else if (typeId == "token")
            {
                foundToken = true;
                Assert.Equal("sample", part.GetProperty("roleId").GetString());
                Assert.Equal("content3",
                    part.GetProperty("content").GetProperty("value")
                        .GetString());
            }
        }
        Assert.True(foundComment);
        Assert.True(foundToken);
    }

    [Fact]
    public async Task ReadAsync_TypeFilter_ReturnsOnlyMatchingParts()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        ItemPartFilter filter = new();
        filter.Clauses.Add(new ItemPartFilterClause { TypeId = "token" });

        using JsonDocument? doc = await reader.ReadAsync("item2", filter);

        Assert.NotNull(doc);
        JsonElement parts = doc.RootElement.GetProperty("parts");
        Assert.Equal(1, parts.GetArrayLength());
        Assert.Equal("token",
            parts[0].GetProperty("typeId").GetString());
    }

    [Fact]
    public async Task ReadAsync_TypeAndRoleFilter_ReturnsOnlyMatchingParts()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        ItemPartFilter filter = new();
        filter.Clauses.Add(new ItemPartFilterClause
        {
            TypeId = "token",
            RoleId = "sample"
        });

        using JsonDocument? doc = await reader.ReadAsync("item2", filter);

        Assert.NotNull(doc);
        JsonElement parts = doc.RootElement.GetProperty("parts");
        Assert.Equal(1, parts.GetArrayLength());
        Assert.Equal("sample", parts[0].GetProperty("roleId").GetString());
    }

    [Fact]
    public async Task ReadAsync_RoleFilterEmpty_MatchesOnlyPartsWithNoRole()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        ItemPartFilter filter = new();
        filter.Clauses.Add(new ItemPartFilterClause
        {
            TypeId = "token",
            RoleId = ""
        });

        using JsonDocument? doc = await reader.ReadAsync("item2", filter);

        Assert.NotNull(doc);
        JsonElement parts = doc.RootElement.GetProperty("parts");
        // part3 has typeId=token but roleId=sample, so it should NOT match
        Assert.Equal(0, parts.GetArrayLength());
    }

    [Fact]
    public async Task ReadAsync_InvertedFilter_ExcludesMatchingParts()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        ItemPartFilter filter = new()
        {
            IsInverted = true
        };
        filter.Clauses.Add(new ItemPartFilterClause { TypeId = "comment" });

        using JsonDocument? doc = await reader.ReadAsync("item2", filter);

        Assert.NotNull(doc);
        JsonElement parts = doc.RootElement.GetProperty("parts");
        Assert.Equal(1, parts.GetArrayLength());
        Assert.Equal("token", parts[0].GetProperty("typeId").GetString());
    }

    [Fact]
    public async Task ReadAsync_ItemWithSinglePart_ReturnsSinglePart()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        using MongoItemJsonReader reader = GetReader();

        using JsonDocument? doc = await reader.ReadAsync("item3", null);

        Assert.NotNull(doc);
        JsonElement parts = doc.RootElement.GetProperty("parts");
        Assert.Equal(1, parts.GetArrayLength());
        Assert.Equal("note", parts[0].GetProperty("typeId").GetString());
    }
}
