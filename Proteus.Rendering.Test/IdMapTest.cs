using Xunit;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Proteus.Rendering.Test;

public sealed class IdMapTest
{
    [Fact]
    public void Reset_ClearsAllMappings()
    {
        IdMap map = new();
        map.MapSourceId("prefix_suffix");

        map.Reset();

        Assert.Equal(0, map.Count);
        Assert.Null(map.GetMappedId("prefix_suffix"));
        Assert.Null(map.GetSourceId(1));
    }

    [Fact]
    public void Reset_WithSeedTrue_ResetsSeed()
    {
        IdMap map = new();
        map.MapSourceId("id1");

        map.Reset(true);
        int newId = map.MapSourceId("id1");

        // after resetting seed, numbering starts from 1 again
        Assert.Equal(1, newId);
    }

    [Fact]
    public void MapSourceId_AssignsIncrementalIds()
    {
        IdMap map = new();

        int id1 = map.MapSourceId("prefix_1");
        int id2 = map.MapSourceId("prefix_2");
        int id3 = map.MapSourceId("prefix_3");

        Assert.Equal(1, id1);
        Assert.Equal(2, id2);
        Assert.Equal(3, id3);
        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void MapSourceId_ReturnsSameIdForSameSource()
    {
        IdMap map = new();

        int firstId = map.MapSourceId("test_id");
        int secondId = map.MapSourceId("test_id");

        Assert.Equal(firstId, secondId);
        Assert.Equal(1, map.Count);
    }

    [Fact]
    public void GetMappedId_ReturnsCorrectIdForSource()
    {
        IdMap map = new();
        int id = map.MapSourceId("source_id");

        int? result = map.GetMappedId("source_id");

        Assert.Equal(id, result);
    }

    [Fact]
    public void GetMappedId_ReturnsNullForUnknownSource()
    {
        IdMap map = new();

        int? result = map.GetMappedId("unknown");

        Assert.Null(result);
    }

    [Fact]
    public void GetSourceId_ReturnsCorrectSourceForId()
    {
        IdMap map = new();
        const string source = "test_source";
        int id = map.MapSourceId(source);

        string? result = map.GetSourceId(id);

        Assert.Equal(source, result);
    }

    [Fact]
    public void GetSourceId_ReturnsNullForUnknownId()
    {
        IdMap map = new();

        string? result = map.GetSourceId(999);

        Assert.Null(result);
    }

    [Fact]
    public void Count_ReturnsCorrectNumberOfMappings()
    {
        IdMap map = new();

        map.MapSourceId("id1");
        map.MapSourceId("id2");
        map.MapSourceId("id3");
        map.MapSourceId("id1"); // duplicate, shouldn't increase count

        Assert.Equal(3, map.Count);
    }

    [Fact]
    public void MapSourceId_ThrowsArgumentNullException_WhenIdIsNull()
    {
        IdMap map = new();

        Assert.Throws<ArgumentNullException>(() => map.MapSourceId(null!));
    }

    [Fact]
    public void GetMappedId_ThrowsArgumentNullException_WhenIdIsNull()
    {
        IdMap map = new();

        Assert.Throws<ArgumentNullException>(() => map.GetMappedId(null!));
    }

    [Fact]
    public async Task MapSourceId_ThreadSafety_GeneratesUniqueIds()
    {
        IdMap map = new();
        const int iterations = 1000;
        List<Task<int>> tasks = [];

        for (int i = 0; i < iterations; i++)
        {
            string id = $"source_{i}";
            tasks.Add(Task.Run(() => map.MapSourceId(id)));
        }

        await Task.WhenAll(tasks);

        var uniqueIds = tasks.Select(t => t.Result).Distinct();
        Assert.Equal(iterations, uniqueIds.Count());
        Assert.Equal(iterations, map.Count);
    }

    [Fact]
    public void ToString_ReturnsExpectedFormat()
    {
        IdMap map = new();
        map.MapSourceId("id1");
        map.MapSourceId("id2");

        string result = map.ToString();

        Assert.Equal("IdMap: 2", result);
    }
}
