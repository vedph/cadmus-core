using Cadmus.Cli.Core;
using Cadmus.Core.Storage;
using Cadmus.Core;
using System.IO;
using System;
using Cadmus.Export.Config;

namespace Cadmus.Migration.Cli.Services;

/// <summary>
/// CLI context service.
/// </summary>
internal sealed class AppContextService(CadmusMigCliContextServiceConfig config)
{
    private readonly CadmusMigCliContextServiceConfig _config = config
        ?? throw new ArgumentNullException(nameof(config));

    /// <summary>
    /// Gets the preview factory provider with the specified plugin tag
    /// (assuming that the plugin has a (single) implementation of
    /// <see cref="ICadmusRenderingFactoryProvider"/>).
    /// </summary>
    /// <param name="pluginTag">The tag of the component in its plugin,
    /// or null to use the standard preview factory provider.</param>
    /// <returns>The provider.</returns>
    public static ICadmusRenderingFactoryProvider? GetPreviewFactoryProvider(
        string? pluginTag = null)
    {
        if (pluginTag == null)
            return new StandardRenderingFactoryProvider();

        return PluginFactoryProvider
            .GetFromTag<ICadmusRenderingFactoryProvider>(pluginTag);
    }

    /// <summary>
    /// Gets the cadmus repository.
    /// </summary>
    /// <param name="tag">The tag.</param>
    /// <returns>Repository</returns>
    /// <exception cref="FileNotFoundException">Repository provider not
    /// found.</exception>
    public ICadmusRepository GetCadmusRepository(string? tag)
    {
        if (tag == null)
        {
            return new AppRepositoryProvider()
            {
                ConnectionString = _config.ConnectionString!
            }.CreateRepository();
        }

        IRepositoryProvider? provider = PluginFactoryProvider
            .GetFromTag<IRepositoryProvider>(tag);
        if (provider == null)
        {
            throw new FileNotFoundException(
                "The requested repository provider tag " + tag +
                " was not found among plugins in " +
                PluginFactoryProvider.GetPluginsDir());
        }
        provider.ConnectionString = _config.ConnectionString!;
        return provider.CreateRepository();
    }
}

/// <summary>
/// Configuration for <see cref="AppContextService"/>.
/// </summary>
public class CadmusMigCliContextServiceConfig
{
    /// <summary>
    /// Gets or sets the connection string to the database.
    /// </summary>
    public string? ConnectionString { get; set; }
}
