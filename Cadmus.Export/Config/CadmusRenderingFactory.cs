using Cadmus.Export.Renderers;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proteus.Core.Text;
using Proteus.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cadmus.Export.Config;

/// <summary>
/// Rendering components factory.
/// </summary>
/// <remarks>The JSON configuration has the following sections:
/// <list type="bullet">
/// <item>
/// <term><c>ContextSuppliers</c></term>
/// <description>List of renderer context suppliers, each named with a key,
/// and having its component ID and eventual options. The key is an arbitrary
/// string, used in the scope of the configuration to reference each filter from
/// other sections.</description>
/// </item>
/// <item>
/// <term><c>TextTreeFilters</c></term>
/// <description>List of text tree filters, each named with a key, and having
/// its component ID and eventual options. The key is an arbitrary string,
/// used in the scope of the configuration to reference each filter from
/// other sections.</description>
/// </item>
/// <item>
/// <term><c>TextFilters</c></term>
/// <description>List of text filters, each named with a key, and having
/// its component ID and eventual options. The key is an arbitrary string,
/// used in the scope of the configuration to reference each filter from
/// other sections.</description>
/// </item>
/// <item>
/// <term><c>JsonRenderers</c></term>
/// <description>List of JSON renderers, each named with a key, and having
/// its component ID and eventual options. The key corresponds to the part
/// type ID, eventually followed by <c>|</c> and its role ID in the case
/// of a layer part. This allows mapping each part type to a specific
/// renderer ID. This key is used in the scope of the configuration to
/// reference each filter from other sections. Under options, any renderer
/// can have a <c>FilterKeys</c> property which is an array of filter keys,
/// representing the filters used by that renderer, to be applied in the
/// specified order.</description>
/// </item>
/// <item>
/// <term><c>TextPartFlatteners</c></term>
/// <description>List of text part flatteners, each named with a key, and
/// having its component ID and eventual options. The key is an arbitrary
/// string, used in the scope of the configuration to reference each filter
/// from other sections.</description>
/// </item>
/// <item>
/// <term><c>TextTreeRenderers</c></term>
/// <description>List of text tree renderers, each named with a key, and
/// having its component ID and eventual options. The key is an arbitrary
/// string, used in the scope of the configuration to reference each filter
/// from other sections.</description>
/// </item>
/// <item>
/// <term><c>ItemComposers</c></term>
/// <description>List of item composers, each named with a key, and having
/// its component ID and eventual options. The key is an arbitrary string,
/// not used elsewhere in the context of the configuration. It is used as
/// an argument for UI which process data export. Each composer can have
/// among its options a <c>TextPartFlattenerKey</c>, a <c>TextTreeFilterKeys</c>,
/// a <c>TextBlockRendererKey</c> and a <c>JsonRendererKeys</c>, referencing
/// the corresponding components by their key.</description>
/// </item>
/// <item>
/// <term><c>ItemIdCollector</c></term>
/// <description>A single item ID collector to use when required. It has
/// the component ID, and eventual options.</description>
/// </item>
/// </list>
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="CadmusRenderingFactory" />
/// class.
/// </remarks>
/// <param name="host">The host.</param>
public class CadmusRenderingFactory(IHost host) : ComponentFactory(host)
{
    /// <summary>
    /// The name of the connection string property to be supplied
    /// in POCO option objects (<c>ConnectionString</c>).
    /// </summary>
    public const string CONNECTION_STRING_NAME = "ConnectionString";

    /// <summary>
    /// The optional general connection string to supply to any component
    /// requiring an option named <see cref="CONNECTION_STRING_NAME"/>
    /// (=<c>ConnectionString</c>), when this option is not specified
    /// in its configuration.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Overrides the options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="section">The section.</param>
    protected override void OverrideOptions(object options,
        IConfigurationSection? section)
    {
        Type optionType = options.GetType();

        // if we have a default connection AND the options type
        // has a ConnectionString property, see if we should supply a value
        // for it
        PropertyInfo? property;
        if (ConnectionString != null &&
            (property = optionType.GetProperty(CONNECTION_STRING_NAME)) != null)
        {
            // here we can safely discard the returned object as it will
            // be equal to the input options, which is not null
            SupplyProperty(optionType, property, options, ConnectionString);
        }
    }

    /// <summary>
    /// Configures the container services to use components from
    /// <c>Pythia.Core</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="additionalAssemblies">The optional additional
    /// assemblies.</param>
    /// <exception cref="ArgumentNullException">container</exception>
    public static void ConfigureServices(IServiceCollection services,
        params Assembly[] additionalAssemblies)
    {
        ArgumentNullException.ThrowIfNull(services);

        // https://simpleinjector.readthedocs.io/en/latest/advanced.html?highlight=batch#batch-registration
        Assembly[] assemblies =
        [
            // Cadmus.Export
            typeof(XsltJsonRenderer).Assembly
        ];
        if (additionalAssemblies?.Length > 0)
            assemblies = [.. assemblies, .. additionalAssemblies];

        // register the components for the specified interfaces
        // from all the assemblies
        foreach (Type it in new[]
        {
            typeof(ICadmusRendererContextSupplier),
            typeof(IJsonRenderer),
            typeof(ICadmusTextTreeRenderer),
            typeof(ITextPartFlattener),
            typeof(ITextTreeFilter),
            typeof(ITextFilter),
            typeof(IItemComposer),
            typeof(IItemIdCollector),
        })
        {
            foreach (Type t in GetAssemblyConcreteTypes(assemblies, it))
            {
                services.AddTransient(it, t);
            }
        }
    }

    private HashSet<string> CollectKeys(string collectionPath)
    {
        HashSet<string> keys = [];
        foreach (var entry in
            ComponentFactoryConfigEntry.ReadComponentEntries(
            Configuration, collectionPath)
            .Where(e => e.Keys?.Count > 0))
        {
            foreach (string id in entry.Keys!) keys.Add(id);
        }
        return keys;
    }

    /// <summary>
    /// Gets all the keys registered for JSON renderers in the
    /// configuration of this factory. This is used by client code
    /// to determine for which Cadmus objects a preview is available.
    /// </summary>
    /// <returns>List of unique keys.</returns>
    public HashSet<string> GetJsonRendererKeys()
        => CollectKeys("JsonRenderers");

    /// <summary>
    /// Gets all the keys registered for JSON text part flatteners
    /// in the configuration of this factory. This is used by client code
    /// to determine for which Cadmus objects a preview is available.
    /// </summary>
    /// <returns>List of unique keys.</returns>
    public HashSet<string> GetFlattenerKeys()
        => CollectKeys("TextPartFlatteners");

    /// <summary>
    /// Gets all the keys registered for item composers in the configuration
    /// of this factory.
    /// </summary>
    /// <returns>List of unique keys.</returns>
    public HashSet<string> GetComposerKeys()
        => CollectKeys("ItemComposers");

    private List<ITextFilter> GetTextFilters(string path)
    {
        IConfigurationSection filterKeys = Configuration.GetSection(path);
        if (filterKeys.Exists())
        {
            string[] keys = filterKeys.Get<string[]>() ?? [];
            return [.. GetTextFilters(keys)];
        }
        return [];
    }

    /// <summary>
    /// Gets the renderer context supplier with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>The supplier or null if not found.</returns>
    public ICadmusRendererContextSupplier? GetRendererContextSupplier(string key)
    {
        return GetComponents<ICadmusRendererContextSupplier>("ContextSuppliers",
            null, [key]).FirstOrDefault();
    }

    /// <summary>
    /// Gets the JSON renderer with the specified key. The renderer can
    /// specify filters in its <c>Options:FilterKeys</c> array property.
    /// </summary>
    /// <param name="key">The key of the requested renderer.</param>
    /// <returns>Renderer or null if not found.</returns>
    public IJsonRenderer? GetJsonRenderer(string key)
    {
        IList<ComponentFactoryConfigEntry> entries =
            ComponentFactoryConfigEntry.ReadComponentEntries(
            Configuration, "JsonRenderers");

        ComponentFactoryConfigEntry? entry =
            entries.FirstOrDefault(e => e.Keys?.Contains(key) == true);
        if (entry == null) return null;

        IJsonRenderer? renderer = GetComponent<IJsonRenderer>(
            entry.Tag!, entry.OptionsPath);
        if (renderer == null) return null;

        // add filters if specified in Options:FilterKeys
        foreach (ITextFilter filter in GetTextFilters(
            entry.OptionsPath + ":FilterKeys"))
        {
            renderer.Filters.Add(filter);
        }

        return renderer;
    }

    /// <summary>
    /// Gets the text tree renderer with the specified key.
    /// </summary>
    /// <param name="key">The key of the requested renderer.</param>
    /// <returns>Renderer or null if not found.</returns>
    public ICadmusTextTreeRenderer? GetTextTreeRenderer(string key)
    {
        IList<ComponentFactoryConfigEntry> entries =
            ComponentFactoryConfigEntry.ReadComponentEntries(
            Configuration, "TextTreeRenderers");

        ComponentFactoryConfigEntry? entry =
            entries.FirstOrDefault(e => e.Keys?.Contains(key) == true);
        if (entry == null) return null;

        ICadmusTextTreeRenderer? renderer = GetComponent<ICadmusTextTreeRenderer>
            (entry.Tag!, entry.OptionsPath);
        if (renderer == null) return null;

        // add filters if specified in Options:FilterKeys
        renderer.Filters.AddRange(GetTextFilters(
            entry.OptionsPath + ":FilterKeys"));

        return renderer;
    }

    /// <summary>
    /// Gets the text part flattener with the specified key.
    /// </summary>
    /// <param name="key">The key of the requested flattener.</param>
    /// <returns>Flattener or null if not found.</returns>
    public ITextPartFlattener? GetTextPartFlattener(string key)
    {
        return GetComponents<ITextPartFlattener>("TextPartFlatteners",
            null, [key]).FirstOrDefault();
    }

    /// <summary>
    /// Gets the text tree filter with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Filter or null if not found.</returns>
    public ITextTreeFilter? GetTextTreeFilter(string key)
    {
        return GetComponents<ITextTreeFilter>("TextTreeFilters",
            null, [key]).FirstOrDefault();
    }

    /// <summary>
    /// Gets the text filters matching any of the specified keys.
    /// Filters are listed under section <c>TextFilters</c>, each with
    /// one or more keys.
    /// Then, these keys are used to include post-rendition filters by
    /// listing one or more of them in the <c>FilterKeys</c> option,
    /// an array of strings.
    /// </summary>
    /// <param name="keys">The desired keys.</param>
    /// <returns>Dictionary with keys and filters.</returns>
    public IList<ITextFilter> GetTextFilters(IList<string> keys) =>
        GetRequiredComponents<ITextFilter>("TextFilters", null, keys);

    /// <summary>
    /// Gets an item composer by key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <returns>Composer or null.</returns>
    /// <exception cref="ArgumentNullException">key</exception>
    public IItemComposer? GetComposer(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        // ItemComposers: match by key
        IList<ComponentFactoryConfigEntry> entries =
            ComponentFactoryConfigEntry.ReadComponentEntries(
            Configuration, "ItemComposers");

        ComponentFactoryConfigEntry? entry =
            entries.FirstOrDefault(e => e.Keys?.Contains(key) == true);
        if (entry == null) return null;

        // instantiate composer
        IItemComposer? composer = GetComponent<IItemComposer>(
            entry.Tag!, entry.OptionsPath);
        if (composer == null) return null;

        // add renderer context suppliers if specified in Options:ContextSupplierKeys
        IConfigurationSection section = Configuration.GetSection(
            entry.OptionsPath + ":ContextSupplierKeys");
        if (section.Exists())
        {
            foreach (string sKey in section.Get<string[]>()!)
            {
                ICadmusRendererContextSupplier? supplier =
                    GetRendererContextSupplier(sKey);
                if (supplier != null) composer.ContextSuppliers.Add(supplier);
            }
        }

        // add text part flattener if specified in Options:TextPartFlattenerKey
        section = Configuration.GetSection(
            entry.OptionsPath + ":TextPartFlattenerKey");
        if (section.Exists())
        {
            string cKey = section.Get<string>()!;
            composer.TextPartFlattener = GetTextPartFlattener(cKey);
        }

        // add text tre filters if specified in Options:TextTreeFilterKeys
        section = Configuration.GetSection(
            entry.OptionsPath + ":TextTreeFilterKeys");
        if (section.Exists())
        {
            foreach (string cKey in section.Get<string[]>()!)
            {
                ITextTreeFilter? filter = GetTextTreeFilter(cKey);
                if (filter != null) composer.TextTreeFilters.Add(filter);
            }
        }

        // add text tree renderer if specified in Options:TextTreeRendererKey
        section = Configuration.GetSection(
        entry.OptionsPath + ":TextTreeRendererKey");
        if (section.Exists())
        {
            string cKey = section.Get<string>()!;
            composer.TextTreeRenderer = GetTextTreeRenderer(cKey);
        }

        // add renderers if specified in Options.JsonRendererKeys
        section = Configuration.GetSection(
            entry.OptionsPath + ":JsonRendererKeys");
        if (section.Exists())
        {
            foreach (string cKey in section.Get<string[]>()!)
            {
                IJsonRenderer? renderer = GetJsonRenderer(cKey);
                if (renderer != null) composer.JsonRenderers[cKey] = renderer;
            }
        }

        return composer;
    }

    /// <summary>
    /// Gets the item identifiers collector if any.
    /// </summary>
    /// <returns>The collector defined in this factory configuration,
    /// or null.</returns>
    public IItemIdCollector? GetItemIdCollector() =>
        GetComponent<IItemIdCollector>("ItemIdCollector", false);
}
