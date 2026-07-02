using Cadmus.Cli.Core;
using Cadmus.Core.Config;
using Cadmus.Seed;
using Cadmus.Seed.General.Parts;
using Cadmus.Seed.Philology.Parts;
using Fusi.Microsoft.Extensions.Configuration.InMemoryJson;
using Fusi.Tools.Config;
using Microsoft.Extensions.Configuration;
using SimpleInjector;
using System.Reflection;

namespace Cadmus.Cli.Plugin.General
{
    /// <summary>
    /// CLI philologic parts seeder factory provider.
    /// </summary>
    /// <seealso cref="ICliPartSeederFactoryProvider" />
    [Obsolete("Replace CLI providers with API providers")]
    [Tag("part-seeder-factory-provider.philology")]
    public sealed class PhilologyCliPartSeederFactoryProvider
        : ICliPartSeederFactoryProvider
    {
        public PartSeederFactory GetFactory(string profile)
        {
            if (profile == null)
                throw new ArgumentNullException(nameof(profile));

            // build the tags to types map for parts/fragments
            Assembly[] seedAssemblies = new[]
            {
                // Cadmus.Seed.General.Parts
                typeof(NotePartSeeder).Assembly,
                // Cadmus.Seed.Philology.Parts
                typeof(ApparatusLayerFragmentSeeder).Assembly,
            };
            TagAttributeToTypeMap map = new();
            map.Add(seedAssemblies);

            // build the container for seeders
            Container container = new();
            PartSeederFactory.ConfigureServices(
                container,
                new StandardPartTypeProvider(map),
                seedAssemblies);

            container.Verify();

            // load seed configuration
            IConfigurationBuilder builder = new ConfigurationBuilder()
                .AddInMemoryJson(profile);
            var configuration = builder.Build();

            return new PartSeederFactory(container, configuration);
        }
    }
}
