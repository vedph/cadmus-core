using Npgsql;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Graph.Ef.PgSql;

/// <summary>
/// PostgreSQL graph database schema.
/// </summary>
public static class PgSqlGraphSchema
{
    private const string SCHEMA_LOCK_KEY = "cadmus-graph-schema";

    private static string LoadResource(string name)
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream($"Cadmus.Graph.Ef.PgSql.Assets.{name}")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static readonly Lazy<string> _updateSql =
        new(() => LoadResource("SchemaUpdate.pgsql"));

    private static readonly Lazy<string> _sql =
        new(() => LoadResource("Schema.pgsql") + "\n" + _updateSql.Value);

    /// <summary>
    /// Gets the SQL code for creating a new graph database: tables, preset
    /// data, indexes and procedures.
    /// </summary>
    /// <returns>SQL.</returns>
    public static string GetSql() => _sql.Value;

    /// <summary>
    /// Gets the idempotent SQL code for upgrading an existing graph database,
    /// creating its missing indexes and replacing its procedures. This is
    /// also included in <see cref="GetSql"/>.
    /// </summary>
    /// <returns>SQL.</returns>
    public static string GetUpdateSql() => _updateSql.Value;

    /// <summary>
    /// Applies the update schema (<see cref="GetUpdateSql"/>) to the existing
    /// database targeted by the specified connection string. This is safe
    /// for concurrent callers, which are serialized, and changes nothing on
    /// up-to-date databases. On big legacy databases it may take some time,
    /// as it creates the missing indexes.
    /// </summary>
    /// <param name="connectionString">The connection string to the graph
    /// database.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">connectionString</exception>
    public static async Task ApplyAsync(string connectionString,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        // schema operations are rare, and may follow the database creation,
        // which would leave pooled connections broken: so they use
        // dedicated connections
        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            Pooling = false
        };

        await using NpgsqlConnection connection = new(builder.ConnectionString);
        await connection.OpenAsync(cancel);
        await using NpgsqlTransaction tr =
            await connection.BeginTransactionAsync(cancel);

        await using (NpgsqlCommand lockCmd = new(
            "SELECT pg_advisory_xact_lock(hashtext($1))", connection, tr))
        {
            lockCmd.Parameters.Add(new NpgsqlParameter { Value = SCHEMA_LOCK_KEY });
            await lockCmd.ExecuteNonQueryAsync(cancel);
        }

        await using (NpgsqlCommand cmd = new(GetUpdateSql(), connection, tr))
        {
            // creating indexes on big legacy tables may be slow
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync(cancel);
        }
        await tr.CommitAsync(cancel);
    }
}
