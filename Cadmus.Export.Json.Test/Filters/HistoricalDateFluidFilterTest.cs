using Cadmus.Export.Json.Filters;
using Fusi.Antiquity.Chronology;
using System.Text.Json.Nodes;

namespace Cadmus.Export.Json.Test.Filters;

public sealed class HistoricalDateFluidFilterTest
{
    private static JsonTemplateNodeMapper CreateMapper()
    {
        JsonTemplateNodeMapper mapper = new();
        mapper.Filters.AddFilter("_hd", new HistoricalDateFluidFilter().Apply);
        return mapper;
    }

    [Fact]
    public void Apply_DatationA_ToNumber()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = CreateMapper();

        // date is 123 AD whose JSON serialized form is:
        // {
        //   "a": {
        //     "value": 123
        //   }
        // }
        const string json = "{\"date\": {\"a\": { \"value\": 123 }}}";

        mapper.Map(json, mapping, target);

        Assert.Equal(123, target["when"]?.GetValue<int>());
    }

    [Fact]
    public void Apply_DatationA_ToText()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd: \"text\" | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = CreateMapper();
        const string json = "{\"date\": {\"a\": { \"value\": 123 }}}";

        mapper.Map(json, mapping, target);

        HistoricalDate expected = new() { A = new Datation { Value = 123 } };
        Assert.Equal(expected.ToString(), target["when"]?.GetValue<string>());
    }

    [Fact]
    public void Apply_SpanDate_AandB_ToNumber()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = CreateMapper();
        const string json = "{\"date\": {\"a\": { \"value\": 100 }, " +
            "\"b\": { \"value\": 150 }}}";

        mapper.Map(json, mapping, target);

        HistoricalDate expected = new()
        {
            A = new Datation { Value = 100 },
            B = new Datation { Value = 150 }
        };
        Assert.Equal((double)target["when"]!.GetValue<decimal>(),
            expected.GetSortValue(), 5);
    }

    [Fact]
    public void Apply_DatationWithAllProperties_ParsesAll()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = CreateMapper();
        const string json = "{\"date\": {\"a\": {" +
            "\"value\": 2," +
            "\"isCentury\": true," +
            "\"isApproximate\": true," +
            "\"isDubious\": true," +
            "\"day\": 15," +
            "\"month\": 6," +
            "\"hint\": \"circa\"" +
            "}}}";

        mapper.Map(json, mapping, target);

        HistoricalDate expected = new()
        {
            A = new Datation
            {
                Value = 2,
                IsCentury = true,
                IsApproximate = true,
                IsDubious = true,
                Day = 15,
                Month = 6,
                Hint = "circa"
            }
        };
        Assert.Equal((double)target["when"]!.GetValue<decimal>(),
            expected.GetSortValue(), 5);
    }

    [Fact]
    public void Apply_StringInput_ParsesAsHistoricalDate()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = CreateMapper();
        const string json = "{\"date\": \"123 AD\"}";

        mapper.Map(json, mapping, target);

        HistoricalDate? expected = HistoricalDate.Parse("123 AD");
        Assert.NotNull(expected);
        Assert.Equal((double)target["when"]!.GetValue<decimal>(),
            expected!.GetSortValue(), 5);
    }

    [Fact]
    public void Apply_MissingSource_ReturnsNull()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = CreateMapper();
        const string json = "{\"other\": 1}";

        mapper.Map(json, mapping, target);

        Assert.False(target.ContainsKey("when"));
    }
}
