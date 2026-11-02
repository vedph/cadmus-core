using Fusi.DbManager.PgSql;
using Microsoft.Extensions.Logging;
using Npgsql;
using System;

namespace Cadmus.Api.Services.Seeding;

/// <summary>
/// PostgreSQL database initializer. This creates a database with its schema
/// when it does not exist, in a way which is safe for concurrent startups
/// (e.g. several API replicas, or an API and a CLI tool) and does not leave
/// half-created databases behind.
/// </summary>
public static class PgSqlDatabaseInitializer
{
    // prefix for the advisory lock key, to avoid collisions with other locks
    private const string LOCK_PREFIX = "cadmus-db-create:";

    private static void ExecuteLockCommand(NpgsqlConnection connection,
        string function, string name)
    {
        using NpgsqlCommand cmd = connection.CreateCommand();
        // hashtext is deterministic across sessions on the same server
        cmd.CommandText = $"SELECT {function}(hashtext(@key));";
        cmd.Parameters.AddWithValue("key", LOCK_PREFIX + name);
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Logs a critical error if a database whose creation failed still
    /// exists, i.e. its removal failed too, as it would be found existing
    /// (but incomplete) at the next startup.
    /// </summary>
    private static void ReportLeftover(PgSqlDbManager manager,
        DatabaseTarget target, ILogger logger)
    {
        try
        {
            if (!manager.Exists(target.Name)) return;
        }
        catch (Exception)
        {
            // server unreachable: nothing more can be told
            return;
        }
        logger.LogCritical("Database {Database} was left partially " +
            "created: drop it manually before restarting", target.Name);
    }

    /// <summary>
    /// Ensures that the specified database exists, creating it with the
    /// specified schema and optional seed SQL code if it does not.
    /// <para>The check and the creation happen while holding a server-wide
    /// advisory lock on the database name, so that concurrent callers do not
    /// race to create the same database: the losers just find it existing.
    /// If creation fails after the database was created (e.g. because of an
    /// error in the schema), <see cref="PgSqlDbManager"/> drops it, so that
    /// a later retry starts from scratch instead of finding an existing but
    /// empty database.
    /// </para>
    /// </summary>
    /// <param name="target">The target database.</param>
    /// <param name="schema">The schema SQL code.</param>
    /// <param name="seed">The optional seed SQL code.</param>
    /// <param name="logger">The logger.</param>
    /// <returns>True if created, false if already existing.</returns>
    /// <exception cref="ArgumentNullException">target or logger</exception>
    public static bool EnsureDatabase(DatabaseTarget target, string schema,
        string? seed, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(logger);

        PgSqlDbManager manager = new(target.Template);

        // a dedicated connection to the maintenance database holds the lock
        // for the whole check-and-create sequence
        using NpgsqlConnection lockConnection = new(
            PgSqlDbManager.GetConnectionString(target.Template, null));
        lockConnection.Open();

        logger.LogDebug("Acquiring creation lock for database {Database}...",
            target.Name);
        ExecuteLockCommand(lockConnection, "pg_advisory_lock", target.Name);

        try
        {
            if (manager.Exists(target.Name))
            {
                logger.LogInformation("Database {Database} already exists",
                    target.Name);
                return false;
            }

            logger.LogInformation("Creating database {Database}...",
                target.Name);
            try
            {
                // this removes the database if its schema or seed fails
                manager.CreateDatabase(target.Name, schema, seed);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error creating database {Database}",
                    target.Name);
                ReportLeftover(manager, target, logger);
                throw;
            }

            logger.LogInformation("Database {Database} created", target.Name);
            return true;
        }
        finally
        {
            // the lock is released anyway when the session ends: never let
            // a failed unlock hide the original exception
            try
            {
                ExecuteLockCommand(lockConnection, "pg_advisory_unlock",
                    target.Name);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Unable to release creation lock for database {Database}",
                    target.Name);
            }
        }
    }
}
