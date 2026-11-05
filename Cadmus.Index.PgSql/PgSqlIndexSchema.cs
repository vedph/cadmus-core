using Npgsql;
using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index.PgSql;

/// <summary>
/// PostgreSQL index schema. The schema script is idempotent: it creates
/// the schema in a new database, and upgrades the schema of databases
/// created by older versions.
/// </summary>
public static class PgSqlIndexSchema
{
    // keys for advisory locks
    private const string CREATE_LOCK_PREFIX = "cadmus-db-create:";
    private const string SCHEMA_LOCK_KEY = "cadmus-index-schema";

    private static readonly Lazy<string> _sql = new(() =>
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Cadmus.Index.PgSql.Assets.Schema.pgsql")!,
            Encoding.UTF8);
        return reader.ReadToEnd();
    });

    /// <summary>
    /// Gets the schema SQL code.
    /// </summary>
    /// <returns>SQL code.</returns>
    public static string GetSql() => _sql.Value;

    /// <summary>
    /// Applies the schema to the database targeted by the specified
    /// connection string, creating or upgrading its tables and indexes.
    /// This is safe for concurrent callers, which are serialized.
    /// Note that upgrading a legacy database rewrites its tables, which
    /// may take some time for big indexes.
    /// </summary>
    /// <param name="connectionString">The connection string to the index
    /// database.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">connectionString</exception>
    public static async Task ApplyAsync(string connectionString,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using NpgsqlConnection connection = new(
            GetUnpooledConnectionString(connectionString));
        await connection.OpenAsync(cancel);
        await using NpgsqlTransaction tr =
            await connection.BeginTransactionAsync(cancel);

        await using (NpgsqlCommand lockCmd = new(
            "SELECT pg_advisory_xact_lock(hashtext($1))", connection, tr))
        {
            lockCmd.Parameters.Add(new NpgsqlParameter { Value = SCHEMA_LOCK_KEY });
            await lockCmd.ExecuteNonQueryAsync(cancel);
        }

        await using (NpgsqlCommand cmd = new(GetSql(), connection, tr))
        {
            // upgrading big legacy tables may be slow
            cmd.CommandTimeout = 0;
            await cmd.ExecuteNonQueryAsync(cancel);
        }
        await tr.CommitAsync(cancel);
    }

    /// <summary>
    /// Gets the name of the database from the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>Name.</returns>
    /// <exception cref="InvalidOperationException">no database name
    /// </exception>
    public static string GetDatabaseName(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder = new(connectionString);
        if (string.IsNullOrEmpty(builder.Database))
        {
            throw new InvalidOperationException(
                "No database name in index connection string");
        }
        return builder.Database;
    }

    // schema operations are rare, and may follow the database creation or
    // removal, which would leave pooled connections broken: so they use
    // dedicated connections
    private static string GetUnpooledConnectionString(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            Pooling = false
        };
        return builder.ConnectionString;
    }

    private static string GetAdminConnectionString(string connectionString)
    {
        NpgsqlConnectionStringBuilder builder = new(connectionString)
        {
            Database = "postgres",
            Pooling = false
        };
        return builder.ConnectionString;
    }

    /// <summary>
    /// Determines whether the database targeted by the specified connection
    /// string exists.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>True if exists.</returns>
    public static async Task<bool> ExistsAsync(string connectionString,
        CancellationToken cancel = default)
    {
        string name = GetDatabaseName(connectionString);
        await using NpgsqlConnection admin = new(
            GetAdminConnectionString(connectionString));
        await admin.OpenAsync(cancel);
        return await ExistsAsync(admin, name, cancel);
    }

    private static async Task<bool> ExistsAsync(NpgsqlConnection admin,
        string name, CancellationToken cancel)
    {
        await using NpgsqlCommand cmd = new(
            "SELECT 1 FROM pg_database WHERE datname = $1", admin);
        cmd.Parameters.Add(new NpgsqlParameter { Value = name });
        return await cmd.ExecuteScalarAsync(cancel) != null;
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection,
        string sql, CancellationToken cancel, string? key = null)
    {
        await using NpgsqlCommand cmd = new(sql, connection);
        if (key != null) cmd.Parameters.Add(new NpgsqlParameter { Value = key });
        await cmd.ExecuteNonQueryAsync(cancel);
    }

    /// <summary>
    /// Ensures that the database targeted by the specified connection string
    /// exists with an up-to-date schema. If the database does not exist, it
    /// is created with its schema and the optional initialization SQL code;
    /// if this fails, the database is dropped. Otherwise, the schema is
    /// applied to bring it up to date.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="initSql">The optional SQL code to execute after creating
    /// the database.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>True if the database was created.</returns>
    /// <exception cref="ArgumentNullException">connectionString</exception>
    public static async Task<bool> EnsureDatabaseAsync(string connectionString,
        string? initSql = null, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        string name = GetDatabaseName(connectionString);
        bool created = false;

        await using (NpgsqlConnection admin = new(
            GetAdminConnectionString(connectionString)))
        {
            await admin.OpenAsync(cancel);
            // a session lock serializes concurrent creators
            await ExecuteAsync(admin, "SELECT pg_advisory_lock(hashtext($1))",
                cancel, CREATE_LOCK_PREFIX + name);
            try
            {
                if (!await ExistsAsync(admin, name, cancel))
                {
                    await ExecuteAsync(admin, "CREATE DATABASE " +
                        PgSqlText.QuoteIdentifier(name), cancel);
                    created = true;
                }
            }
            finally
            {
                await ExecuteAsync(admin,
                    "SELECT pg_advisory_unlock(hashtext($1))",
                    CancellationToken.None, CREATE_LOCK_PREFIX + name);
            }

            try
            {
                await ApplyAsync(connectionString, cancel);
                if (created && !string.IsNullOrWhiteSpace(initSql))
                {
                    await using NpgsqlConnection connection =
                        new(GetUnpooledConnectionString(connectionString));
                    await connection.OpenAsync(cancel);
                    await ExecuteAsync(connection, initSql, cancel);
                }
            }
            catch (Exception) when (created)
            {
                // do not leave a half-created database behind
                await ExecuteAsync(admin, "DROP DATABASE IF EXISTS " +
                    PgSqlText.QuoteIdentifier(name) + " WITH (FORCE)",
                    CancellationToken.None);
                throw;
            }
        }
        return created;
    }
}
