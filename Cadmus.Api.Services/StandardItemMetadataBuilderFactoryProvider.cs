using Cadmus.Core.Config;
using Cadmus.General.Parts;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Cadmus.Api.Services;

/// <summary>
/// Standard implementation of <see cref="IItemMetadataBuilderFactoryProvider"/>.
/// </summary>
public sealed class StandardItemMetadataBuilderFactoryProvider :
    IItemMetadataBuilderFactoryProvider
{
    private readonly string _connectionString;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="StandardItemMetadataBuilderFactoryProvider"/> class.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    public StandardItemMetadataBuilderFactoryProvider(string connectionString)
    {
        _connectionString = connectionString ??
            throw new ArgumentNullException(nameof(connectionString));
    }

    private static IHost GetHost(string config)
    {
        // build the tags to types map for parts/fragments
        Assembly[] browserAssemblies =
        [
            // Cadmus.General.Parts
            typeof(NotePart).Assembly,
        ];
        TagAttributeToTypeMap map = new();
        map.Add(browserAssemblies);

        return new HostBuilder()
            .ConfigureServices((hostContext, services) =>
            {
                ItemMetadataBuilderFactory.ConfigureServices(services,
                    new StandardPartTypeProvider(map),
                    browserAssemblies);
            })
            // extension method from Fusi library
            .AddInMemoryJson(config)
            .Build();
    }

    /// <summary>
    /// Gets the item metadata builder factory.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Factory.</returns>
    /// <exception cref="ArgumentNullException">profile</exception>
    public ItemMetadataBuilderFactory GetFactory(string profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        return new ItemMetadataBuilderFactory(GetHost(profile), _connectionString);
    }
}
