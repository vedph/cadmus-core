using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.Mongo;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Proteus.Core.Regions;
using Proteus.Entries.Export;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cadmus.Import.Proteus;

/// <summary>
/// Mongo-based Cadmus entry set exporter.
/// <para>When <see cref="MongoEntrySetExporterOptions.IsMergeEnabled"/> is
/// true, items already present in the database are merged with the exported
/// ones: their parts are added to the existing item, replacing any existing
/// part with the same type and role IDs. This allows importing items in
/// several passes, where later passes add parts to items imported earlier.
/// </para>
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
            if (_options.IsMergeEnabled && MergeItem(item)) continue;

            // write item without parts
            _repository!.AddItem(item, !_options.NoHistory);

            // write parts
            foreach (IPart part in item.Parts)
                _repository.AddPart(part, !_options.NoHistory);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets information about all the parts of the item with the specified ID.
    /// </summary>
    /// <param name="itemId">The item ID.</param>
    /// <returns>Parts information.</returns>
    private List<PartInfo> GetItemPartInfos(string itemId)
    {
        List<PartInfo> infos = [];
        PartFilter filter = new()
        {
            ItemIds = [itemId],
            PageSize = 100
        };

        while (true)
        {
            DataPage<PartInfo> page = _repository!.GetParts(filter);
            infos.AddRange(page.Items);
            if (page.Items.Count == 0 || infos.Count >= page.Total) break;
            filter.PageNumber++;
        }
        return infos;
    }

    private static bool AreRolesEqual(string? a, string? b) =>
        string.IsNullOrEmpty(a) ? string.IsNullOrEmpty(b) : a == b;

    /// <summary>
    /// Merges the specified item into the item with the same ID already
    /// stored in the database, if any. All the parts of <paramref name="item"/>
    /// are added to the stored item; when the stored item already has a
    /// part with the same type and role IDs, that part is replaced.
    /// </summary>
    /// <param name="item">The item to merge.</param>
    /// <returns>True if the item existed and was merged; false if it does
    /// not exist, and thus it must be added as a new item.</returns>
    private bool MergeItem(IItem item)
    {
        // parts are not required here, and could not be instantiated anyway
        // as this repository has no part types map
        IItem? oldItem = _repository!.GetItem(item.Id, false);
        if (oldItem == null) return false;

        bool history = !_options!.NoHistory;
        List<PartInfo> oldParts = GetItemPartInfos(item.Id);

        foreach (IPart part in item.Parts)
        {
            PartInfo? oldPart = oldParts.Find(p => p.TypeId == part.TypeId
                && AreRolesEqual(p.RoleId, part.RoleId));

            if (oldPart != null)
            {
                Logger?.LogInformation("Replacing part {OldPartId} with " +
                    "{NewPartId} ({TypeId}:{RoleId}) in item {ItemId}",
                    oldPart.Id, part.Id, part.TypeId, part.RoleId, item.Id);

                // when IDs are equal, AddPart just replaces the old part
                if (oldPart.Id != part.Id)
                    _repository.DeletePart(oldPart.Id!, part.UserId, history);
                oldParts.Remove(oldPart);
            }

            _repository.AddPart(part, history);
        }

        // update the existing item (its parts are stored separately)
        oldItem.UserId = item.UserId;
        _repository.AddItem(oldItem, history);

        Logger?.LogInformation("Merged {PartCount} part(s) into existing " +
            "item {ItemId}", item.Parts.Count, item.Id);

        return true;
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

    /// <summary>
    /// Gets or sets a value indicating whether items being exported should
    /// be merged with items with the same ID already present in the database.
    /// When true, if an item already exists, the parts of the item being
    /// exported are added to it, replacing any existing part with the same
    /// type and role IDs; otherwise, the item is added as a new one. This is
    /// useful when importing items in several passes, where later passes add
    /// parts to items imported earlier.
    /// </summary>
    public bool IsMergeEnabled { get; set; }
}
