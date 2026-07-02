using Fusi.Tools.Configuration;
using McMaster.NETCore.Plugins;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

// https://github.com/natemcmaster/DotNetCorePlugins

namespace Cadmus.Cli.Core;

/// <summary>
/// Plugins-based provider for Cadmus factory providers. This is used to
/// load a factory provider from an external plugin.
/// </summary>
public static class PluginFactoryProvider
{
    /// <summary>
    /// Gets the default plugins directory, corresponding to the app's
    /// base directory plus <c>plugins</c>.
    /// </summary>
    /// <returns>Directory.</returns>
    public static string GetPluginsDir() =>
        Path.Combine(AppContext.BaseDirectory, "plugins");

    /// <summary>
    /// Scans all the plugins in the plugins folder and returns the first
    /// plugin matching the requested tag.
    /// </summary>
    /// <param name="tag">The requested plugin tag, or null to match the
    /// first plugin of type <typeparamref name="T"/>, whatever its tag.
    /// </param>
    /// <param name="pluginDir">The optional plugins directory. When not
    /// specified, this is got from <see cref="GetPluginsDir"/>.</param>
    /// <returns>The provider, or null if not found.</returns>
    public static T? GetFromTag<T>(string? tag, string? pluginDir = null)
        where T : class
    {
        // create plugin loaders
        pluginDir ??= GetPluginsDir();

        foreach (string dir in Directory.GetDirectories(pluginDir))
        {
            string dirName = Path.GetFileName(dir);
            string pluginDll = Path.Combine(dir, dirName + ".dll");

            Debug.WriteLine(
                $"Probing {pluginDll} for {typeof(T)} with tag {tag}");
            T? provider = Get<T>(pluginDll, tag);
            if (provider != null)
            {
                Debug.WriteLine("Plugin found");
                return provider;
            }
            else
            {
                Debug.WriteLine("Plugin not found!");
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the provider plugin from the specified directory.
    /// </summary>
    /// <param name="pluginPath">The path to the plugin file.</param>
    /// <param name="tag">The optional plugin tag. If null, the first
    /// matching plugin in the target assembly will be returned. This can
    /// be used when an assembly just contains a single plugin implementation.
    /// </param>
    /// <returns>Provider, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">path</exception>
    public static T? Get<T>(string pluginPath, string? tag = null)
        where T : class
    {
        if (pluginPath == null)
            throw new ArgumentNullException(nameof(pluginPath));

        if (!File.Exists(pluginPath)) return null;

        PluginLoader loader = PluginLoader.CreateFromAssemblyFile(
                pluginPath,
                sharedTypes: new[] { typeof(T) });

        foreach (Type type in loader.LoadDefaultAssembly()
            .GetExportedTypes()
            .Where(t => typeof(T).IsAssignableFrom(t) && !t.IsAbstract))
        {
            if (tag == null)
                return (T?)Activator.CreateInstance(type);

            TagAttribute? tagAttr = (TagAttribute?)
                Attribute.GetCustomAttribute(type, typeof(TagAttribute));
            if (tagAttr?.Tag == tag)
                return (T?)Activator.CreateInstance(type);
        }

        return null;
    }
}
