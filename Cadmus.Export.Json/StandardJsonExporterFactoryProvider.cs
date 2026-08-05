using Cadmus.Export.Config;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Cadmus.Export.Json;

/// <summary>
/// Standard implementation of <see cref="IJsonExporterFactoryProvider"/>.
/// <para>Tag: <c>it.vedph.json-exporter-factory-provider.standard</c>.</para>
/// </summary>
[Tag("it.vedph.json-exporter-factory-provider.standard")]
public sealed class StandardJsonExporterFactoryProvider : IJsonExporterFactoryProvider
{
    private static IHost GetHost(string config, Assembly[] assemblies)
    {
        return new HostBuilder()
            .ConfigureServices((hostContext, services) =>
                JsonExporterFactory.ConfigureServices(services, assemblies))
            // extension method from Fusi library
            .AddInMemoryJson(config)
            .Build();
    }

    /// <summary>
    /// Gets the factory for the specified profile.
    /// </summary>
    /// <param name="profile">The profile name.</param>
    /// <param name="additionalAssemblies">Additional assemblies to consider.</param>
    /// <returns>The <see cref="JsonExporterFactory"/> instance.</returns>
    public JsonExporterFactory GetFactory(string profile,
        params Assembly[] additionalAssemblies)
    {
        return new JsonExporterFactory(GetHost(profile, additionalAssemblies));
    }
}
