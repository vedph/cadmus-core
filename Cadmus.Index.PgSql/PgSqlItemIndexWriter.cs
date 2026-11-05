using Cadmus.Core;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index.PgSql;

/// <summary>
/// PostgreSQL item index writer.
/// <para>Tag: <c>item-index-writer.pgsql</c>.</para>
/// </summary>
/// <remarks>
/// <para>Each write operation is sent to the database as a single batch,
/// i.e. in a single round trip, executed as an implicit transaction: so,
/// an item or part is either fully indexed or not indexed at all. Writes
/// affecting the same item are serialized by means of transaction-level
/// advisory locks, so that concurrent saves cannot duplicate pins.</para>
/// <para>Values longer than their target column are truncated, rather
/// than making the whole write fail.</para>
/// </remarks>
[Tag("item-index-writer.pgsql")]
public class PgSqlItemIndexWriter : IItemIndexWriter,
    IConfigurable<PgSqlIndexOptions>
{
    /// <summary>
    /// The maximum count of items written in a single batch by
    /// <see cref="WriteItemsAsync"/>.
    /// </summary>
    public const int BATCH_SIZE = 100;

    // the first key 1128350808 (0x43414458="CADX") is the class of the
    // advisory locks on items, whose second key is the item ID hash
    private const string LOCK_SQL =
        "SELECT pg_advisory_xact_lock(1128350808, h) " +
        "FROM (SELECT DISTINCT hashtext(x) AS h FROM unnest($1::text[]) " +
        "AS u(x) ORDER BY h) AS t";

    private const string UPSERT_ITEMS_SQL =
        "INSERT INTO item (id, title, description, facet_id, group_id, " +
        "sort_key, flags, time_created, creator_id, time_modified, user_id) " +
        "SELECT * FROM unnest($1::text[], $2::text[], $3::text[], " +
        "$4::text[], $5::text[], $6::text[], $7::int4[], $8::timestamptz[], " +
        "$9::text[], $10::timestamptz[], $11::text[]) " +
        "ON CONFLICT (id) DO UPDATE SET title=EXCLUDED.title, " +
        "description=EXCLUDED.description, facet_id=EXCLUDED.facet_id, " +
        "group_id=EXCLUDED.group_id, sort_key=EXCLUDED.sort_key, " +
        "flags=EXCLUDED.flags, time_created=EXCLUDED.time_created, " +
        "creator_id=EXCLUDED.creator_id, time_modified=EXCLUDED.time_modified, " +
        "user_id=EXCLUDED.user_id " +
        // do not create a new row version when nothing changed
        "WHERE (item.title, item.description, item.facet_id, item.group_id, " +
        "item.sort_key, item.flags, item.time_created, item.creator_id, " +
        "item.time_modified, item.user_id) IS DISTINCT FROM " +
        "(EXCLUDED.title, EXCLUDED.description, EXCLUDED.facet_id, " +
        "EXCLUDED.group_id, EXCLUDED.sort_key, EXCLUDED.flags, " +
        "EXCLUDED.time_created, EXCLUDED.creator_id, EXCLUDED.time_modified, " +
        "EXCLUDED.user_id)";

    private const string DELETE_PART_PINS_SQL =
        "DELETE FROM pin WHERE part_id = ANY($1::text[])";

    private const string INSERT_PINS_SQL =
        "INSERT INTO pin (item_id, part_id, part_type_id, part_role_id, " +
        "name, value, time_indexed) " +
        "SELECT u.*, now() FROM unnest($1::text[], $2::text[], $3::text[], " +
        "$4::text[], $5::text[], $6::text[]) AS u";

    // column sizes
    private const int ID_MAX = 36;
    private const int TITLE_MAX = 500;
    private const int DESCRIPTION_MAX = 1000;
    private const int SHORT_MAX = 100;
    private const int SORT_KEY_MAX = 1000;
    private const int VALUE_MAX = 500;

    // connection strings whose database was ensured in this process
    private static readonly ConcurrentDictionary<string, Lazy<Task>> _ensured
        = new();

    private string _cs = "";
    private int _timeout = 30;

    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    /// <exception cref="ArgumentNullException">value</exception>
    public string ConnectionString
    {
        get => _cs;
        set => _cs = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Gets or sets the optional initialization context. This is a string
    /// with SQL code to execute after the database gets created.
    /// </summary>
    public object? InitContext { get; set; }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(PgSqlIndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ConnectionString = options.ConnectionString ?? "";
        _timeout = Math.Max(options.CommandTimeout, 0);
    }

    /// <summary>
    /// Gets the PostgreSQL schema used to populate a created database.
    /// </summary>
    /// <returns>SQL code.</returns>
    public static string GetPgSqlSchema() => PgSqlIndexSchema.GetSql();

    /// <summary>
    /// Forgets which databases were ensured to exist in this process, so
    /// that the next write ensures them again. This is useful when a
    /// database is dropped by other means.
    /// </summary>
    public static void ResetEnsuredDatabases() => _ensured.Clear();

    private async Task EnsureIndexAsync(CancellationToken cancel)
    {
        string cs = ConnectionString;
        if (string.IsNullOrEmpty(cs))
        {
            throw new InvalidOperationException(
                "No connection string configured for the PostgreSQL index");
        }

        // ensure once per process, sharing the work among concurrent callers
        string? initSql = InitContext as string;
        Lazy<Task> ensure = _ensured.GetOrAdd(cs, key => new Lazy<Task>(
            () => PgSqlIndexSchema.EnsureDatabaseAsync(key, initSql,
                CancellationToken.None)));
        try
        {
            await ensure.Value.WaitAsync(cancel);
        }
        catch (Exception) when (!cancel.IsCancellationRequested)
        {
            // let the next call retry
            _ensured.TryRemove(new KeyValuePair<string, Lazy<Task>>(cs, ensure));
            throw;
        }
    }

    /// <summary>
    /// Creates the index database if it does not exist, and brings its
    /// schema up to date. This is done only once per process for each
    /// connection string.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    public Task CreateIndexAsync(CancellationToken cancel = default) =>
        EnsureIndexAsync(cancel);

    #region Batch
    /// <summary>
    /// The rows to be written in a single batch.
    /// </summary>
    private sealed class WriteBatch
    {
        private readonly HashSet<string> _itemIds = [];

        // item columns
        public List<string> Ids { get; } = [];
        public List<string> Titles { get; } = [];
        public List<string> Descriptions { get; } = [];
        public List<string> FacetIds { get; } = [];
        public List<string?> GroupIds { get; } = [];
        public List<string> SortKeys { get; } = [];
        public List<int> Flags { get; } = [];
        public List<DateTime> TimesCreated { get; } = [];
        public List<string?> CreatorIds { get; } = [];
        public List<DateTime> TimesModified { get; } = [];
        public List<string?> UserIds { get; } = [];

        // parts whose pins must be replaced
        public HashSet<string> PartIds { get; } = [];

        // pin columns
        public List<string> PinItemIds { get; } = [];
        public List<string> PinPartIds { get; } = [];
        public List<string> PinPartTypeIds { get; } = [];
        public List<string?> PinRoleIds { get; } = [];
        public List<string> PinNames { get; } = [];
        public List<string> PinValues { get; } = [];

        public int ItemCount => Ids.Count;

        public bool HasItem(string id) => _itemIds.Contains(id);

        public void AddItem(IItem item)
        {
            _itemIds.Add(item.Id);
            Ids.Add(item.Id);
            Titles.Add(PgSqlText.Fit(item.Title, TITLE_MAX));
            Descriptions.Add(PgSqlText.Fit(item.Description, DESCRIPTION_MAX));
            FacetIds.Add(PgSqlText.Fit(item.FacetId, SHORT_MAX));
            GroupIds.Add(PgSqlText.FitOrNull(item.GroupId, SHORT_MAX));
            SortKeys.Add(PgSqlText.Fit(item.SortKey, SORT_KEY_MAX));
            Flags.Add(item.Flags);
            TimesCreated.Add(PgSqlText.ToUtc(item.TimeCreated));
            CreatorIds.Add(PgSqlText.FitOrNull(item.CreatorId, SHORT_MAX));
            TimesModified.Add(PgSqlText.ToUtc(item.TimeModified));
            UserIds.Add(PgSqlText.FitOrNull(item.UserId, SHORT_MAX));
        }

        public void AddPart(IItem item, IPart part)
        {
            ValidateId(part.Id, "part");
            PartIds.Add(part.Id);

            string typeId = PgSqlText.Fit(part.TypeId, SHORT_MAX);
            foreach (DataPin pin in part.GetDataPins(item))
            {
                // a pin without a name cannot be searched
                if (string.IsNullOrEmpty(pin.Name)) continue;

                // pins always belong to the item and part being written:
                // this guarantees that they are replaced on the next write
                PinItemIds.Add(item.Id);
                PinPartIds.Add(part.Id);
                PinPartTypeIds.Add(typeId);
                PinRoleIds.Add(PgSqlText.FitOrNull(pin.RoleId, SHORT_MAX));
                PinNames.Add(PgSqlText.Fit(pin.Name, SHORT_MAX));
                PinValues.Add(PgSqlText.Fit(pin.Value, VALUE_MAX));
            }
        }
    }

    private static void ValidateId(string? id, string kind)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException($"The {kind} has no ID");
        if (id.Length > ID_MAX)
        {
            throw new ArgumentException(
                $"The {kind} ID \"{id}\" is longer than {ID_MAX} characters");
        }
    }

    private static NpgsqlParameter ArrayParam<T>(List<T> values,
        NpgsqlDbType type) => new()
        {
            Value = values.ToArray(),
            NpgsqlDbType = NpgsqlDbType.Array | type
        };

    private static NpgsqlBatchCommand Command(string sql,
        params NpgsqlParameter[] parameters)
    {
        NpgsqlBatchCommand cmd = new(sql);
        cmd.Parameters.AddRange(parameters);
        return cmd;
    }

    private async Task ExecuteAsync(WriteBatch batch, CancellationToken cancel)
    {
        if (batch.ItemCount == 0) return;

        await EnsureIndexAsync(cancel);

        await using NpgsqlConnection connection =
            await PgSqlDataSources.Get(ConnectionString)
                .OpenConnectionAsync(cancel);
        // a batch is executed as an implicit transaction
        await using NpgsqlBatch command = new(connection)
        {
            Timeout = _timeout
        };

        command.BatchCommands.Add(Command(LOCK_SQL,
            ArrayParam(batch.Ids, NpgsqlDbType.Text)));

        command.BatchCommands.Add(Command(UPSERT_ITEMS_SQL,
            ArrayParam(batch.Ids, NpgsqlDbType.Text),
            ArrayParam(batch.Titles, NpgsqlDbType.Text),
            ArrayParam(batch.Descriptions, NpgsqlDbType.Text),
            ArrayParam(batch.FacetIds, NpgsqlDbType.Text),
            ArrayParam(batch.GroupIds, NpgsqlDbType.Text),
            ArrayParam(batch.SortKeys, NpgsqlDbType.Text),
            ArrayParam(batch.Flags, NpgsqlDbType.Integer),
            ArrayParam(batch.TimesCreated, NpgsqlDbType.TimestampTz),
            ArrayParam(batch.CreatorIds, NpgsqlDbType.Text),
            ArrayParam(batch.TimesModified, NpgsqlDbType.TimestampTz),
            ArrayParam(batch.UserIds, NpgsqlDbType.Text)));

        if (batch.PartIds.Count > 0)
        {
            command.BatchCommands.Add(Command(DELETE_PART_PINS_SQL,
                ArrayParam([.. batch.PartIds], NpgsqlDbType.Text)));
        }

        if (batch.PinNames.Count > 0)
        {
            command.BatchCommands.Add(Command(INSERT_PINS_SQL,
                ArrayParam(batch.PinItemIds, NpgsqlDbType.Text),
                ArrayParam(batch.PinPartIds, NpgsqlDbType.Text),
                ArrayParam(batch.PinPartTypeIds, NpgsqlDbType.Text),
                ArrayParam(batch.PinRoleIds, NpgsqlDbType.Text),
                ArrayParam(batch.PinNames, NpgsqlDbType.Text),
                ArrayParam(batch.PinValues, NpgsqlDbType.Text)));
        }

        await command.ExecuteNonQueryAsync(cancel);
    }
    #endregion

    private static WriteBatch CreateItemBatch(IItem item)
    {
        ValidateId(item.Id, "item");
        WriteBatch batch = new();
        batch.AddItem(item);
        if (item.Parts?.Count > 0)
        {
            foreach (IPart part in item.Parts) batch.AddPart(item, part);
        }
        return batch;
    }

    /// <summary>
    /// Writes the specified item and all its parts (if any) to the index.
    /// The item is inserted or updated; the pins of each of its parts
    /// replace the pins previously indexed for the same part, while the
    /// pins of parts not included in the item are left untouched.
    /// If the index does not exist, it is created.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">item</exception>
    /// <exception cref="ArgumentException">invalid item or part ID
    /// </exception>
    public Task WriteItemAsync(IItem item, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return ExecuteAsync(CreateItemBatch(item), cancel);
    }

    /// <summary>
    /// Bulk-writes the specified items, in batches of up to
    /// <see cref="BATCH_SIZE"/> items each. Each batch is atomic.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="progress">The optional progress reporter. The percent
    /// is reported only when the count of items is known in advance.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">items</exception>
    public async Task WriteItemsAsync(IEnumerable<IItem> items,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(items);

        int total = items is IReadOnlyCollection<IItem> collection
            ? collection.Count : 0;
        ProgressReport report = new();
        WriteBatch batch = new();
        int written = 0;

        async Task FlushAsync()
        {
            await ExecuteAsync(batch, cancel);
            if (progress != null && batch.ItemCount > 0)
            {
                written += batch.ItemCount;
                report.Count = written;
                report.Percent = total > 0
                    ? (int)(written * 100L / total) : 0;
                progress.Report(report);
            }
            batch = new WriteBatch();
        }

        foreach (IItem item in items)
        {
            cancel.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(item, nameof(items));
            ValidateId(item.Id, "item");

            // the same item cannot be upserted twice in the same statement
            if (batch.ItemCount >= BATCH_SIZE || batch.HasItem(item.Id))
                await FlushAsync();

            batch.AddItem(item);
            if (item.Parts?.Count > 0)
            {
                foreach (IPart part in item.Parts) batch.AddPart(item, part);
            }
        }
        await FlushAsync();
    }

    /// <summary>
    /// Writes the specified part to the index, replacing the pins previously
    /// indexed for it. Its item is inserted or updated too.
    /// </summary>
    /// <param name="item">The item the part belongs to.</param>
    /// <param name="part">The part.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">item or part</exception>
    /// <exception cref="ArgumentException">invalid item or part ID
    /// </exception>
    public Task WritePartAsync(IItem item, IPart part,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(part);

        ValidateId(item.Id, "item");
        WriteBatch batch = new();
        batch.AddItem(item);
        batch.AddPart(item, part);
        return ExecuteAsync(batch, cancel);
    }

    private async Task ExecuteNonQueryAsync(string sql, string id,
        CancellationToken cancel)
    {
        await EnsureIndexAsync(cancel);

        await using NpgsqlConnection connection =
            await PgSqlDataSources.Get(ConnectionString)
                .OpenConnectionAsync(cancel);
        await using NpgsqlCommand cmd = new(sql, connection)
        {
            CommandTimeout = _timeout
        };
        cmd.Parameters.Add(new NpgsqlParameter
        {
            Value = id,
            NpgsqlDbType = NpgsqlDbType.Text
        });
        await cmd.ExecuteNonQueryAsync(cancel);
    }

    /// <summary>
    /// Deletes the item with the specified identifier with all its pins.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">itemId</exception>
    public Task DeleteItemAsync(string itemId,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        // pins are deleted by cascade
        return ExecuteNonQueryAsync("DELETE FROM item WHERE id = $1",
            itemId, cancel);
    }

    /// <summary>
    /// Deletes the pins of the part with the specified ID from the index.
    /// </summary>
    /// <param name="partId">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">partId</exception>
    public Task DeletePartAsync(string partId,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(partId);
        return ExecuteNonQueryAsync("DELETE FROM pin WHERE part_id = $1",
            partId, cancel);
    }

    /// <summary>
    /// Clears the whole index, if it exists.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    public async Task ClearAsync(CancellationToken cancel = default)
    {
        try
        {
            await using NpgsqlConnection connection =
                await PgSqlDataSources.Get(ConnectionString)
                    .OpenConnectionAsync(cancel);
            await using NpgsqlCommand cmd = new(
                "TRUNCATE pin, item RESTART IDENTITY", connection)
            {
                CommandTimeout = _timeout
            };
            await cmd.ExecuteNonQueryAsync(cancel);
        }
        catch (PostgresException ex) when (ex.SqlState is
            PostgresErrorCodes.InvalidCatalogName or
            PostgresErrorCodes.UndefinedTable)
        {
            // no database or no tables: nothing to clear
        }
    }
}
