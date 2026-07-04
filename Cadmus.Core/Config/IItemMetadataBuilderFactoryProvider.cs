namespace Cadmus.Core.Config;

/// <summary>
/// Item metadata builders factory provider. This is used to provide an
/// <see cref="ItemMetadataBuilderFactory"/> from a specified profile file.
/// </summary>
public interface IItemMetadataBuilderFactoryProvider
{
    /// <summary>
    /// Gets the metadata builders seeders factory.
    /// </summary>
    /// <param name="profile">The profile.</param>
    /// <returns>Factory.</returns>
    ItemMetadataBuilderFactory GetFactory(string profile);
}
