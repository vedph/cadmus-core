using Cadmus.Core.Config;
using Cadmus.Index.Config;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Index.Test;

public sealed class ItemIndexFactoryTest
{
    private const string CS = "Server=localhost;Database=x;";

    private static ItemIndexFactory GetFactory(string profile)
    {
        IHost host = new HostBuilder()
            .ConfigureServices((_, services) =>
                ItemIndexFactory.ConfigureServices(services,
                    typeof(ItemIndexFactoryTest).Assembly))
            .AddInMemoryJson(profile)
            .Build();
        return new ItemIndexFactory(host, CS);
    }

    private const string PROFILE = """
        {
          "flags": [
            { "id": 1, "label": "to revise", "colorKey": "F08080" },
            { "id": "bad", "label": "skipped" },
            { "id": 2, "label": "checked", "description": "Checked." }
          ],
          "index": {
            "writer": { "id": "test-index-writer" },
            "reader": { "id": "test-index-reader" }
          }
        }
        """;

    [Fact]
    public void GetFlagDefinitions_Ok()
    {
        IList<FlagDefinition> flags = GetFactory(PROFILE).GetFlagDefinitions();

        Assert.Equal(2, flags.Count);
        Assert.Equal(1, flags[0].Id);
        Assert.Equal("to revise", flags[0].Label);
        Assert.Equal("F08080", flags[0].ColorKey);
        Assert.Equal(2, flags[1].Id);
        Assert.Equal("Checked.", flags[1].Description);
    }

    [Fact]
    public void GetFlagDefinitions_None_Empty()
    {
        Assert.Empty(GetFactory("{}").GetFlagDefinitions());
    }

    [Fact]
    public void GetItemIndexReader_SuppliesOptions()
    {
        TestConfigurableReader? reader = GetFactory(PROFILE)
            .GetItemIndexReader() as TestConfigurableReader;

        Assert.NotNull(reader);
        Assert.Equal(CS, reader.Options!.ConnectionString);
        Assert.Equal(2, reader.Options.FlagDefinitions!.Count);
    }

    [Fact]
    public void GetItemIndexReader_ExplicitFlags_Kept()
    {
        const string profile = """
            {
              "flags": [ { "id": 1, "label": "a" } ],
              "index": {
                "reader": {
                  "id": "test-index-reader",
                  "options": {
                    "flagDefinitions": [ { "id": 4, "label": "b" } ]
                  }
                }
              }
            }
            """;
        TestConfigurableReader reader = (TestConfigurableReader)
            GetFactory(profile).GetItemIndexReader()!;

        FlagDefinition flag = Assert.Single(reader.Options!.FlagDefinitions!);
        Assert.Equal(4, flag.Id);
    }

    [Fact]
    public void GetItemIndexWriter_SetsInitContext()
    {
        TestConfigurableWriter writer = (TestConfigurableWriter)
            GetFactory(PROFILE).GetItemIndexWriter("SELECT 1;")!;

        Assert.Equal(CS, writer.Options!.ConnectionString);
        Assert.Equal("SELECT 1;", writer.InitContext);
    }

    [Fact]
    public void GetComponents_NotConfigured_Null()
    {
        ItemIndexFactory factory = GetFactory("{}");
        Assert.Null(factory.GetItemIndexReader());
        Assert.Null(factory.GetItemIndexWriter());
    }
}
