using Cadmus.Export;
using Cadmus.Export.Json.Filters;
using Cadmus.Export.Mapping;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Export.Json.Test;

public sealed class JsonExporterTest
{
    private sealed class FakeItemIdCollector(params string[] ids) : IItemIdCollector
    {
        private readonly string[] _ids = ids;

        public IEnumerable<string> GetIds() => _ids;
    }

    private sealed class FakeItemJsonReader(IDictionary<string, string> jsonByItemId)
        : IItemJsonReader
    {
        private readonly IDictionary<string, string> _json = jsonByItemId;

        public Task<JsonDocument?> ReadAsync(string itemId, ItemPartFilter? filter)
            => Task.FromResult(_json.TryGetValue(itemId, out string? json)
                ? JsonDocument.Parse(json) : null);

        public void Dispose() { }
    }

    [Fact]
    public async Task ExportAsync_NoMappings_YieldsEmptyObjectPerItem()
    {
        FakeItemIdCollector collector = new("i1");
        FakeItemJsonReader reader = new(
            new Dictionary<string, string> { ["i1"] = "{\"_id\": \"i1\"}" });
        JsonExporter exporter = new(reader, collector);

        List<JsonDocument> docs = [];
        await foreach (JsonDocument doc in exporter.ExportAsync(CancellationToken.None))
            docs.Add(doc);

        JsonDocument doc0 = Assert.Single(docs);
        Assert.Equal(JsonValueKind.Object, doc0.RootElement.ValueKind);
        Assert.Empty(doc0.RootElement.EnumerateObject());
    }

    [Fact]
    public async Task ExportAsync_WithMappingAndFilter_TransformsEachItem()
    {
        FakeItemIdCollector collector = new("i1", "i2");
        FakeItemJsonReader reader = new(new Dictionary<string, string>
        {
            ["i1"] = "{\"_id\": \"i1\", \"date\": {\"a\": {\"value\": 123}}}",
            ["i2"] = "{\"_id\": \"i2\", \"date\": {\"a\": {\"value\": -50}}}"
        });
        JsonExporter exporter = new(reader, collector)
        {
            TemplateFilters = new Dictionary<string, IFluidFilter>
            {
                ["historical-date"] = new HistoricalDateFluidFilter()
            }
        };
        exporter.Mappings.Add(new JsonNodeMapping
        {
            Source = ".",
            Output = "{\"_id\": {{ value._id | json }}, " +
                "\"when\": {{ value.date | historical-date | json }} }"
        });

        List<JsonDocument> docs = [];
        await foreach (JsonDocument doc in exporter.ExportAsync(CancellationToken.None))
            docs.Add(doc);

        Assert.Equal(2, docs.Count);
        Assert.Equal("i1", docs[0].RootElement.GetProperty("_id").GetString());
        Assert.Equal(123, docs[0].RootElement.GetProperty("when").GetInt32());
        Assert.Equal("i2", docs[1].RootElement.GetProperty("_id").GetString());
        Assert.Equal(-50, docs[1].RootElement.GetProperty("when").GetInt32());
    }

    [Fact]
    public async Task ExportAsync_MissingItem_IsSkipped()
    {
        FakeItemIdCollector collector = new("i1", "missing");
        FakeItemJsonReader reader = new(
            new Dictionary<string, string> { ["i1"] = "{\"_id\": \"i1\"}" });
        JsonExporter exporter = new(reader, collector);
        exporter.Mappings.Add(new JsonNodeMapping
        {
            Source = ".",
            Output = "{\"_id\": {{ value._id | json }} }"
        });

        List<JsonDocument> docs = [];
        await foreach (JsonDocument doc in exporter.ExportAsync(CancellationToken.None))
            docs.Add(doc);

        JsonDocument doc0 = Assert.Single(docs);
        Assert.Equal("i1", doc0.RootElement.GetProperty("_id").GetString());
    }
}
