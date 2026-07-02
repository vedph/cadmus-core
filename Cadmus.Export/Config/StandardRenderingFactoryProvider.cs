using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Hosting;
using System.Reflection;

namespace Cadmus.Export.Config;

/// <summary>
/// Standard preview factory provider.
/// Tag: <c>it.vedph.rendering-factory-provider.standard</c>.
/// </summary>
/// <seealso cref="ICadmusRenderingFactoryProvider" />
[Tag("it.vedph.rendering-factory-provider.standard")]
public sealed class StandardRenderingFactoryProvider : ICadmusRenderingFactoryProvider
{
    private static IHost GetHost(string config, Assembly[] assemblies)
    {
        return new HostBuilder()
            .ConfigureServices((hostContext, services) =>
                CadmusRenderingFactory.ConfigureServices(services, assemblies))
            // extension method from Fusi library
            .AddInMemoryJson(config)
            .Build();
    }

    /// <summary>
    /// Gets the factory.
    /// </summary>
    /// <param name="profile">The JSON configuration profile.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies
    /// to load components from.</param>
    /// <returns>Factory.</returns>
    public CadmusRenderingFactory GetFactory(string profile,
        params Assembly[] additionalAssemblies)
    {
        return new CadmusRenderingFactory(GetHost(profile, additionalAssemblies));
    }
}
