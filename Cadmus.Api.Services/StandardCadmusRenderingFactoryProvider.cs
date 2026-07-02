using Cadmus.Export.Config;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Cadmus.Api.Services;

/// <summary>
/// Standard Cadmus preview factory provider.
/// </summary>
public sealed class StandardCadmusRenderingFactoryProvider :
    ICadmusRenderingFactoryProvider
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
    /// <param name="profile">The profile.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies.
    /// </param>
    /// <returns>Factory.</returns>
    /// <exception cref="ArgumentNullException">profile</exception>
    public CadmusRenderingFactory GetFactory(string profile,
        params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new CadmusRenderingFactory(GetHost(profile, additionalAssemblies));
    }
}
