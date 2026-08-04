using Cadmus.Export.Json;

namespace Cadmus.Export.Json.Test;

public sealed class JsonNodeMappingTest
{
    [Fact]
    public void Clone_CopiesOwnAndBaseProperties()
    {
        JsonNodeMapping mapping = new()
        {
            Name = "test",
            Source = "a.b",
            Sid = "{$part-id}",
            Output = "{\"x\": {{ value | json }} }",
            TargetProperty = "slot",
            Children =
            {
                new JsonNodeMapping { Name = "child", Source = "c" }
            }
        };

        JsonNodeMapping clone = mapping.Clone();

        Assert.NotSame(mapping, clone);
        Assert.Equal(mapping.Name, clone.Name);
        Assert.Equal(mapping.Source, clone.Source);
        Assert.Equal(mapping.Sid, clone.Sid);
        Assert.Equal(mapping.Output, clone.Output);
        Assert.Equal(mapping.TargetProperty, clone.TargetProperty);
        Assert.Single(clone.Children);
        Assert.NotSame(mapping.Children[0], clone.Children[0]);
        Assert.Equal("child", clone.Children[0].Name);
    }

    [Fact]
    public void ToString_IncludesOutputAndTargetProperty()
    {
        JsonNodeMapping mapping = new()
        {
            Id = 1,
            Name = "test",
            Source = "a",
            Output = "{}",
            TargetProperty = "slot"
        };

        string s = mapping.ToString();

        Assert.Contains("=> slot", s);
        Assert.Contains("-> {}", s);
    }
}
