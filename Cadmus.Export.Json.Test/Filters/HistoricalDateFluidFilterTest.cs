using Cadmus.Export.Json.Filters;
using System.Text.Json.Nodes;

namespace Cadmus.Export.Json.Test.Filters;

public sealed class HistoricalDateFluidFilterTest
{
    [Fact]
    public void Apply_DatationA_ToNumber()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "date",
            Output = "{\"when\": {{ value | _hd | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();

        HistoricalDateFluidFilter filter = new();
        mapper.Filters.AddFilter("_hd", filter.Apply);

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
}
