using System;
using System.Reflection;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Cadmus.Mongo;
using Cadmus.Philology.Parts;
using Fusi.Tools.Configuration;

namespace Cadmus.Cli.Services;

/// <summary>
/// Cadmus standard repository service. This includes general and philologic
/// parts.
/// Tag: <c>repository-provider.standard</c>.
/// </summary>
[Tag("repository-provider.standard")]
public sealed class StandardRepositoryProvider : IRepositoryProvider
{
    private readonly IPartTypeProvider _partTypeProvider;

    public string ConnectionString { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="StandardRepositoryProvider"/>
    /// class.
    /// </summary>
    /// <exception cref="ArgumentNullException">configuration</exception>
    public StandardRepositoryProvider()
    {
        ConnectionString = "";
        TagAttributeToTypeMap map = new();
        map.Add(new[]
        {
            // Cadmus.General.Parts
            typeof(NotePart).GetTypeInfo().Assembly,
            // Cadmus.Philology.Parts
            typeof(ApparatusLayerFragment).GetTypeInfo().Assembly,
        });

        _partTypeProvider = new StandardPartTypeProvider(map);
    }

    /// <summary>
    /// Gets the part type provider.
    /// </summary>
    /// <returns>part type provider</returns>
    public IPartTypeProvider GetPartTypeProvider()
    {
        return _partTypeProvider;
    }

    /// <summary>
    /// Creates a Cadmus repository.
    /// </summary>
    /// <returns>repository</returns>
    /// <exception cref="ArgumentNullException">null database</exception>
    public ICadmusRepository CreateRepository()
    {
        // create the repository (no need to use container here)
        MongoCadmusRepository repository =
            new(
                _partTypeProvider,
                new StandardItemSortKeyBuilder());

        repository.Configure(new MongoCadmusRepositoryOptions
        {
            ConnectionString = ConnectionString ??
                throw new InvalidOperationException(
                "No connection string set for IRepositoryProvider implementation")
        });

        return repository;
    }
}
