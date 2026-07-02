using Cadmus.Cli.Core;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Cadmus.Mongo;
using Fusi.Tools.Config;
using System.Reflection;

namespace Cadmus.Cli.Plugin.General
{
    /// <summary>
    /// CLI repository factory provider for general parts.
    /// Tag: <c>repository-factory-provider.general</c>.
    /// </summary>
    /// <seealso cref="ICliRepositoryFactoryProvider" />
    [Obsolete("Replace CLI providers with API providers")]
    [Tag("repository-factory-provider.general")]
    public sealed class GeneralCliCadmusRepositoryProvider :
        ICliCadmusRepositoryProvider
    {
        private readonly IPartTypeProvider _partTypeProvider;

        /// <summary>
        /// Gets or sets the connection string.
        /// </summary>
        public string? ConnectionString { get; set; }

        /// <summary>
        /// Initializes a new instance of the
        /// <see cref="GeneralCliCadmusRepositoryProvider"/> class.
        /// </summary>
        public GeneralCliCadmusRepositoryProvider()
        {
            TagAttributeToTypeMap map = new();
            map.Add(new[]
            {
                // Cadmus.General.Parts
                typeof(NotePart).GetTypeInfo().Assembly,
            });
            _partTypeProvider = new StandardPartTypeProvider(map);
        }

        /// <summary>
        /// Creates the repository.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException">database</exception>
        public ICadmusRepository CreateRepository(string database)
        {
            if (database == null)
                throw new ArgumentNullException(nameof(database));

            // create the repository (no need to use container here)
            MongoCadmusRepository repository =
                new(_partTypeProvider, new StandardItemSortKeyBuilder());

            repository.Configure(new MongoCadmusRepositoryOptions
            {
                ConnectionString = string.Format(ConnectionString!, database)
            });

            return repository;
        }
    }
}