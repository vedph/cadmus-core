using System.Reflection;

namespace Cadmus.Export.Config;

/// <summary>
/// Provider for <see cref="CadmusRenderingFactory"/>.
/// </summary>
public interface ICadmusRenderingFactoryProvider
{
    /// <summary>
    /// Gets the factory.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <param name="additionalAssemblies">The optional additional assemblies
    /// to load components from.</param>
    /// <returns>Factory.</returns>
    CadmusRenderingFactory GetFactory(string profile,
        params Assembly[] additionalAssemblies);
}
