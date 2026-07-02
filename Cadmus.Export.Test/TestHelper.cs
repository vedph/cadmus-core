using Cadmus.Export.Config;
using Cadmus.Export.ML.Renderers;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Hosting;
using Proteus.Text.Filters;
using System.IO;
using System.Reflection;
using System.Text;
using Xunit;

namespace Cadmus.Export.Test;


internal static class TestHelper
{
    public static string CS = "mongodb://localhost:27017/cadmus-test";

    public static string LoadResourceText(string name)
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Cadmus.Export.Test.Assets.{name}")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }
    private static IHost GetHost(string config)
    {
        return new HostBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                CadmusRenderingFactory.ConfigureServices(services,
                    // Cadmus.Export
                    typeof(TeiOffLinearTextTreeRenderer).Assembly,
                    // Proteus.Text.Filters
                    typeof(ReplacerFilter).Assembly);
            })
            // extension method from Fusi library
            .AddInMemoryJson(config)
            .Build();
    }

    public static CadmusRenderingFactory GetFactory()
    {
        return new CadmusRenderingFactory(GetHost(LoadResourceText("Preview.json")))
        {
            ConnectionString = "mongodb://localhost:27017/cadmus-test"
        };
    }
}
