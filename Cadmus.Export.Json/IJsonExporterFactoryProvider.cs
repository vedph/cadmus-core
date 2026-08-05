using System.Reflection;

namespace Cadmus.Export.Json;

/// <summary>
/// Provider for <see cref="JsonExporterFactory"/>.
/// </summary>
public interface IJsonExporterFactoryProvider
{
    /// <summary>
    /// Gets the factory.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies
    /// to load components from.</param>
    /// <returns>Factory.</returns>
    JsonExporterFactory GetFactory(string profile,
        params Assembly[] additionalAssemblies);
}
