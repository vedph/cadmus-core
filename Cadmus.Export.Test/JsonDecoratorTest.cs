using Xunit;

namespace Cadmus.Export.Test;

public sealed class JsonDecoratorTest
{
    private const string JSON = "{\"root\":{" +
        "\"typeId\":\"type\"," +
        "\"roleId\":\"role\"," +
        "\"fragments\":[" +
        "{\"location\":\"1.1\",\"text\":\"alpha\"}," +
        "{\"location\":\"1.2\",\"text\":\"beta\"}]}}";

    [Fact]
    public void DecorateLayerPartFrr_NoLayerPart_Unchanged()
    {
        string? json = JsonDecorator.DecorateLayerPartFrr("{\"x\":1}");
        Assert.NotNull(json);
    }

    [Fact]
    public void DecorateLayerPartFrr_LayerPart_Ok()
    {
        string? json = JsonDecorator.DecorateLayerPartFrr(JSON);
        Assert.NotNull(json);
        Assert.Equal("{\"root\":{" +
            "\"typeId\":\"type\"," +
            "\"roleId\":\"role\"," +
            "\"fragments\":[" +
            "{\"location\":\"1.1\",\"text\":\"alpha\",\"_key\":\"type:role@0\"}," +
            "{\"location\":\"1.2\",\"text\":\"beta\",\"_key\":\"type:role@1\"}]}}", json);
    }
}
