using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Index.Search;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index.PgSql;

/// <summary>
/// PostgreSQL item index reader.
/// <para>Tag: <c>item-index-reader.pgsql</c>.</para>
/// </summary>
/// <remarks>Queries are parsed with <see cref="IndexQueryParser"/> and
/// built with <see cref="PgSqlQueryBuilder"/>. Each search executes the
/// count and the page queries in a single round trip.</remarks>
[Tag("item-index-reader.pgsql")]
public class PgSqlItemIndexReader : IItemIndexReader,
    IConfigurable<PgSqlIndexOptions>
{
    private readonly PgSqlQueryBuilder _builder = new();
    private IndexQueryParser _parser = new();
    private int _timeout = 30;
    private string _cs = "";

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
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(PgSqlIndexOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ConnectionString = options.ConnectionString ?? "";
        _timeout = Math.Max(options.CommandTimeout, 0);
        _parser = new IndexQueryParser(options.FlagDefinitions);
    }

    /// <summary>
    /// Sets the flag definitions used to resolve flag labels in queries.
    /// </summary>
    /// <param name="definitions">The definitions.</param>
    public void SetFlagDefinitions(IEnumerable<FlagDefinition>? definitions)
    {
        _parser = new IndexQueryParser(definitions);
    }

    private static bool IsQueryError(PostgresException ex) => ex.SqlState is
        PostgresErrorCodes.InvalidRegularExpression or
        PostgresErrorCodes.InvalidTextRepresentation or
        PostgresErrorCodes.NumericValueOutOfRange or
        PostgresErrorCodes.StatementTooComplex or
        PostgresErrorCodes.ProgramLimitExceeded or
        PostgresErrorCodes.QueryCanceled;

    private async Task<DataPage<T>> SearchAsync<T>(PgSqlQuery query,
        PagingOptions options, Func<NpgsqlDataReader, T> read,
        CancellationToken cancel)
    {
        await using NpgsqlConnection connection =
            await PgSqlDataSources.Get(ConnectionString)
                .OpenConnectionAsync(cancel);

        // the similarity threshold is set for the transaction only
        await using NpgsqlTransaction? tr = query.SimilarityThreshold.HasValue
            ? await connection.BeginTransactionAsync(cancel)
            : null;

        await using NpgsqlBatch batch = new(connection, tr)
        {
            Timeout = _timeout
        };

        if (query.SimilarityThreshold.HasValue)
        {
            NpgsqlBatchCommand setCmd = new(
                "SELECT set_config('pg_trgm.similarity_threshold', $1, true)");
            setCmd.Parameters.Add(new NpgsqlParameter
            {
                Value = query.SimilarityThreshold.Value.ToString(
                    CultureInfo.InvariantCulture),
                NpgsqlDbType = NpgsqlDbType.Text
            });
            batch.BatchCommands.Add(setCmd);
        }

        NpgsqlBatchCommand countCmd = new(query.CountSql);
        foreach (PgSqlQueryParameter p in query.CountParameters)
            countCmd.Parameters.Add(p.ToNpgsqlParameter());
        batch.BatchCommands.Add(countCmd);

        NpgsqlBatchCommand pageCmd = new(query.PageSql);
        foreach (PgSqlQueryParameter p in query.PageParameters)
            pageCmd.Parameters.Add(p.ToNpgsqlParameter());
        batch.BatchCommands.Add(pageCmd);

        try
        {
            await using NpgsqlDataReader reader =
                await batch.ExecuteReaderAsync(cancel);
            if (query.SimilarityThreshold.HasValue)
                await reader.NextResultAsync(cancel);

            await reader.ReadAsync(cancel);
            int total = (int)Math.Min(reader.GetInt64(0), int.MaxValue);

            await reader.NextResultAsync(cancel);
            List<T> items = [];
            while (await reader.ReadAsync(cancel)) items.Add(read(reader));

            return new DataPage<T>(options.PageNumber, options.PageSize,
                total, items);
        }
        catch (PostgresException ex) when (IsQueryError(ex))
        {
            throw new CadmusQueryException(ex.MessageText, ex);
        }
        catch (NpgsqlException ex) when (ex.InnerException is TimeoutException)
        {
            throw new CadmusQueryException(
                "The query took too long: try making it more selective", ex);
        }
    }

    private static string? GetNullableString(NpgsqlDataReader reader, int i)
        => reader.IsDBNull(i) ? null : reader.GetString(i);

    private static ItemInfo ReadItem(NpgsqlDataReader reader) => new()
    {
        Id = reader.GetString(0),
        Title = reader.GetString(1),
        Description = reader.GetString(2),
        FacetId = reader.GetString(3),
        GroupId = GetNullableString(reader, 4),
        SortKey = reader.GetString(5),
        Flags = reader.GetInt32(6),
        TimeCreated = reader.GetDateTime(7),
        CreatorId = GetNullableString(reader, 8) ?? "",
        TimeModified = reader.GetDateTime(9),
        UserId = GetNullableString(reader, 10) ?? "",
    };

    private static DataPinInfo ReadPin(NpgsqlDataReader reader) => new()
    {
        ItemId = reader.GetString(0),
        PartId = reader.GetString(1),
        PartTypeId = reader.GetString(2),
        RoleId = GetNullableString(reader, 3),
        Name = reader.GetString(4),
        Value = reader.GetString(5),
    };

    /// <summary>
    /// Searches for the items matching the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options. A page size equal to 0
    /// means no paging.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page of results, sorted by item sort key.</returns>
    /// <exception cref="ArgumentNullException">query or options</exception>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    public Task<DataPage<ItemInfo>> SearchItemsAsync(string query,
        PagingOptions options, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        PgSqlQuery sql = _builder.BuildItemQuery(_parser.Parse(query), options);
        return SearchAsync(sql, options, ReadItem, cancel);
    }

    /// <summary>
    /// Searches for the pins matching the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options. A page size equal to 0
    /// means no paging.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page of results, sorted by pin name and value.</returns>
    /// <exception cref="ArgumentNullException">query or options</exception>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    public Task<DataPage<DataPinInfo>> SearchPinsAsync(string query,
        PagingOptions options, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        PgSqlQuery sql = _builder.BuildPinQuery(_parser.Parse(query), options);
        return SearchAsync(sql, options, ReadPin, cancel);
    }
}
