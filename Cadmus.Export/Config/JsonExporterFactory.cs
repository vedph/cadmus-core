using Cadmus.Export.Mapping;
using Cadmus.Export.Renderers;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Reflection;

namespace Cadmus.Export.Config;

/// <summary>
/// Factory for JSON exporter components. This factory is based on a JSON
/// configuration document having the following sections:
/// <list type="bullet">
/// <item>
/// <term><c>source/itemIdCollector</c></term>
/// <description>configurable object for <see cref="IItemIdCollector"/>.
/// </description>
/// </item>
/// <item>
/// <term><c>source/partFilter</c> (optional)</term>
/// <description>JSON serialization of <see cref="ItemPartFilter"/>.</description>
/// </item>
/// <item>
/// <term><c>source/templateFilters</c> (optional)</term>
/// <description>array of template filter configurable objects.</description>
/// </item>
/// <item>
/// <term><c>namedMappings</c> (optional)</term>
/// <description>array of named mappings (defined once and reused via their name).
/// </description>
/// </item>
/// <item>
/// <term><c>mappings</c></term>
/// <description>array of mappings.</description>
/// </item>
/// </list>
/// </summary>
/// <param name="host"></param>
public sealed class JsonExporterFactory(IHost host) : ComponentFactory(host)
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
    /// <c>Cadmus.Export</c>.
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
            typeof(IItemIdCollector),
            typeof(IItemJsonReader),
        })
        {
            foreach (Type t in GetAssemblyConcreteTypes(assemblies, it))
            {
                services.AddTransient(it, t);
            }
        }
    }

    /// <summary>
    /// Gets the item identifiers collector from <c>source/itemIdCollector</c>.
    /// </summary>
    /// <returns>The collector defined in this factory configuration.</returns>
    public IItemIdCollector GetItemIdCollector() =>
        GetComponent<IItemIdCollector>("Source/ItemIdCollector", true)!;
}
