using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Mongo;
using Fusi.Tools.Configuration;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Proteus.Core.Regions;
using Proteus.Entries.Export;
using System;
using System.Threading.Tasks;

namespace Cadmus.Import.Proteus;

/// <summary>
/// Mongo-based Cadmus entry set exporter.
/// <para>Tag: <c>it.vedph.entry-set-exporter.cadmus.mongo</c>.</para>
/// </summary>
[Tag("it.vedph.entry-set-exporter.cadmus.mongo")]
public sealed class MongoEntrySetExporter : EntrySetExporter, IEntrySetExporter,
    IConfigurable<MongoEntrySetExporterOptions>
{
    private MongoEntrySetExporterOptions? _options;
    private MongoCadmusRepository? _repository;
    private string? _currentConnString;

    /// <summary>
    /// The Mongo client. This gets created by <see cref="EnsureClientCreated(string)"/>
    /// and cached until the received connection string changes.
    /// </summary>
    private MongoClient? _client;

    /// <summary>
    /// Creates a new instance of <see cref="MongoEntrySetExporter"/>.
    /// </summary>
    public MongoEntrySetExporter()
    {
        // camel case everything:
        // https://stackoverflow.com/questions/19521626/mongodb-convention-packs/19521784#19521784
        ConventionPack pack = new()
        {
            new CamelCaseElementNameConvention()
        };
        ConventionRegistry.Register("camel case", pack, _ => true);
    }

    /// <summary>
    /// Ensures that <see cref="_client"/> is created for the specified
    /// source.
    /// </summary>
    /// <param name="source">The source (connection string).</param>
    /// <exception cref="ArgumentNullException">source</exception>
    private void EnsureClientCreated(string source)
    {
        ArgumentNullException.ThrowIfNull(source);

        if (_client != null && _currentConnString == source) return;

        _client = new MongoClient(source);
        _currentConnString = source;
    }

    /// <summary>
    /// Configures this exporter with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(MongoEntrySetExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Opens the exporter output. Call this once from outside the pipeline,
    /// when you want to start exporting. This will initialize the database
    /// connection objects.
    /// </summary>
    /// <exception cref="InvalidOperationException">No connection string for
    /// MongoEntrySetExporter</exception>
    protected override Task DoOpenAsync()
    {
        if (string.IsNullOrEmpty(_options?.ConnectionString))
        {
            throw new InvalidOperationException("No connection string for "
                + nameof(MongoEntrySetExporter));
        }
        EnsureClientCreated(_options.ConnectionString);

        // we do not need to configure part types as we are just saving
        _repository = new(new StandardPartTypeProvider(new TagAttributeToTypeMap()),
            new StandardItemSortKeyBuilder());
        _repository.Configure(new MongoCadmusRepositoryOptions
        {
            ConnectionString = _options.ConnectionString
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes the exporter output. Call this once from outside the pipeline,
    /// when you want to end exporting. Here this method does nothing.
    /// </summary>
    protected override Task DoCloseAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// Exports the specified entry set.
    /// </summary>
    /// <param name="entrySet">The entry set.</param>
    /// <param name="regionSet">The entry regions set.</param>
    /// <exception cref="ArgumentNullException"></exception>
    /// <exception cref="InvalidOperationException">
    /// No connection string for MongoEntrySetExporter or
    /// Mongo database DBNAME not open.
    /// </exception>
    protected override Task DoExportAsync(EntrySet entrySet,
        EntryRegionSet regionSet)
    {
        ArgumentNullException.ThrowIfNull(entrySet);
        ArgumentNullException.ThrowIfNull(regionSet);

        if (string.IsNullOrEmpty(_options?.ConnectionString))
        {
            throw new InvalidOperationException("No connection string for "
                + nameof(MongoEntrySetExporter));
        }

        // get context
        CadmusEntrySetContext context = (CadmusEntrySetContext)entrySet.Context;
        if (context.Items.Count == 0) return Task.CompletedTask;

        foreach (IItem item in context.Items)
        {
            // write item without parts
            _repository!.AddItem(item, !_options.NoHistory);

            // write parts
            foreach (IPart part in item.Parts)
                _repository.AddPart(part, !_options.NoHistory);
        }

        return Task.CompletedTask;
    }
}

/// <summary>
/// Options for <see cref="MongoEntrySetExporter"/>.
/// </summary>
public class MongoEntrySetExporterOptions
{
    /// <summary>
    /// Gets or sets the connection string to the Cadmus database to write to.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether imported data should not be
    /// inserted in the history.
    /// </summary>
    public bool NoHistory { get; set; }
}
