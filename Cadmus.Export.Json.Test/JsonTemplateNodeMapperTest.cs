using Cadmus.Export.Json;
using Fluid;
using Fluid.Values;
using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace Cadmus.Export.Json.Test;

public sealed class JsonTemplateNodeMapperTest
{
    [Fact]
    public void Map_ScalarWithTargetProperty_MergesUnderProperty()
    {
        JsonNodeMapping mapping = new()
        {
            Name = "name",
            Source = "name",
            Output = "{\"fullName\": {{ value | json }} }",
            TargetProperty = "person"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"name\": \"Jane\"}";

        mapper.Map(json, mapping, target);

        Assert.Equal("Jane", target["person"]!["fullName"]!.GetValue<string>());
    }

    [Fact]
    public void Map_ObjectWithoutTargetProperty_MergesAtRoot()
    {
        JsonNodeMapping mapping = new()
        {
            Source = ".",
            Output = "{\"greeting\": \"Hello, {{ value.name }}!\"}"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"name\": \"Bob\"}";

        mapper.Map(json, mapping, target);

        Assert.Equal("Hello, Bob!", target["greeting"]!.GetValue<string>());
    }

    [Fact]
    public void Map_SiblingMappingsSameTargetProperty_EnrichObject()
    {
        JsonNodeMapping root = new()
        {
            Source = ".",
            Children =
            {
                new JsonNodeMapping
                {
                    Source = "name",
                    Output = "{\"person\": {\"firstName\": {{ value | json }} }}"
                },
                new JsonNodeMapping
                {
                    Source = "surname",
                    Output = "{\"person\": {\"lastName\": {{ value | json }} }}"
                }
            }
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"name\": \"Jane\", \"surname\": \"Doe\"}";

        mapper.Map(json, root, target);

        JsonObject person = target["person"]!.AsObject();
        Assert.Equal("Jane", person["firstName"]!.GetValue<string>());
        Assert.Equal("Doe", person["lastName"]!.GetValue<string>());
    }

    [Fact]
    public void Map_NestedProperties_MergesUnderProperties()
    {
        JsonNodeMapping mapping = new()
        {
            Source = ".",
            Output = "{\"address\": {" +
            "\"street\": \"{{ value.street }}\", " +
            "\"city\": \"{{ value.city }}\"" +
            "}}"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"street\": \"123 Main St\", \"city\": \"Anytown\"}";

        mapper.Map(json, mapping, target);

        Assert.Equal("123 Main St", target["address"]!["street"]!.GetValue<string>());
        Assert.Equal("Anytown", target["address"]!["city"]!.GetValue<string>());
    }

    [Fact]
    public void Map_ArraySource_ConcatenatesItemsUnderTargetProperty()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "events",
            Output = "[ { \"type\": {{ value.type | json }} } ]",
            TargetProperty = "events"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"events\": [ {\"type\": \"birth\"}, " +
            "{\"type\": \"death\"} ] }";

        mapper.Map(json, mapping, target);

        JsonArray events = target["events"]!.AsArray();
        Assert.Equal(2, events.Count);
        Assert.Equal("birth", events[0]!["type"]!.GetValue<string>());
        Assert.Equal("death", events[1]!["type"]!.GetValue<string>());
    }

    [Fact]
    public void Map_TargetPropertyWithMetadataPlaceholder_IsResolved()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "name",
            Output = "{{ value | json }}",
            TargetProperty = "{$slot}"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        mapper.Data["slot"] = "nickname";
        const string json = "{\"name\": \"Jane\"}";

        mapper.Map(json, mapping, target);

        Assert.Equal("Jane", target["nickname"]!.GetValue<string>());
    }

    [Fact]
    public void Map_JsonFilterWithComplexValue_SerializesWholeValue()
    {
        JsonNodeMapping mapping = new()
        {
            Source = ".",
            Output = "{\"raw\": {{ value | json }} }"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"a\": 1, \"b\": [1,2,3], \"c\": {\"d\": true}}";

        mapper.Map(json, mapping, target);

        JsonObject raw = target["raw"]!.AsObject();
        Assert.Equal(1, raw["a"]!.GetValue<int>());
        Assert.Equal(3, raw["b"]!.AsArray().Count);
        Assert.True(raw["c"]!["d"]!.GetValue<bool>());
    }

    [Fact]
    public void Filters_CustomFilter_IsUsableInTemplate()
    {
        JsonTemplateNodeMapper mapper = new();
        mapper.Filters.AddFilter("shout", (input, _, _) =>
            new ValueTask<FluidValue>(
                new StringValue(input.ToStringValue().ToUpperInvariant())));

        JsonNodeMapping mapping = new()
        {
            Source = "name",
            Output = "{\"name\": {{ value | shout | json }} }"
        };
        JsonObject target = [];
        const string json = "{\"name\": \"jane\"}";

        mapper.Map(json, mapping, target);

        Assert.Equal("JANE", target["name"]!.GetValue<string>());
    }

    [Fact]
    public void BuildOutput_InvalidFluidTemplate_Throws()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "name",
            Output = "{% invalid %}"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"name\": \"Jane\"}";

        Assert.Throws<InvalidOperationException>(
            () => mapper.Map(json, mapping, target));
    }

    [Fact]
    public void BuildOutput_InvalidJsonOutput_Throws()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "name",
            Output = "{ not valid json"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"name\": \"Jane\"}";

        Assert.Throws<InvalidOperationException>(
            () => mapper.Map(json, mapping, target));
    }

    [Fact]
    public void BuildOutput_RootOutputNotObjectWithoutTargetProperty_Throws()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "name",
            Output = "{{ value | json }}"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        const string json = "{\"name\": \"Jane\"}";

        Assert.Throws<InvalidOperationException>(
            () => mapper.Map(json, mapping, target));
    }

    #region Cadmus Item
    private string LoadResourceText(string name)
    {
        string resourceName = $"Cadmus.Export.Json.Test.Assets.{name}";
        using Stream? stream = GetType().Assembly
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Resource '{resourceName}' not found.");
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    [Fact]
    public void BuildOutput_CadmusItem_Categories_ItemsMapped()
    {
        JsonNodeMapping mapping = new()
        {
            Source = "_parts[?typeId=='it.vedph.categories' && roleId=='ins-fn']" +
                ".content.categories",
            Output = "[{{ value | json }}]",
            TargetProperty = "functions"
        };
        JsonObject target = [];
        JsonTemplateNodeMapper mapper = new();
        string json = LoadResourceText("Item.json");
        
        mapper.Map(json, mapping, target);

        // expected functions array property with 2 items
        JsonArray functions = target["functions"]!.AsArray();
        Assert.Equal(2, functions.Count);
    }
    #endregion
}
