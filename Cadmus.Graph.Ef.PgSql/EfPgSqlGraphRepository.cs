using Fusi.DbManager.PgSql;
using Fusi.Tools.Configuration;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Graph.Ef.PgSql;

/// <summary>
/// Entity Framework graph repository for PostgreSQL.
/// <para>Tag: <c>graph-repository.ef-pg</c>.</para>
/// </summary>
/// <seealso cref="EfGraphRepository" />
[Tag("graph-repository.ef-pg")]
public sealed class EfPgSqlGraphRepository : EfGraphRepository, IGraphRepository
{
    private readonly Lock _optionsLock = new();
    private DbContextOptions<CadmusGraphDbContext>? _options;
    private string? _optionsConnectionString;

    /// <summary>
    /// Gets the SQL schema for creating a new graph database.
    /// </summary>
    /// <returns>SQL DDL code.</returns>
    public static string GetSchema() => PgSqlGraphSchema.GetSql();

    /// <summary>
    /// Gets a new DB context configured for <see cref="ConnectionString" />.
    /// </summary>
    /// <returns>DB context.</returns>
    /// <exception cref="InvalidOperationException">No connection string
    /// configured for graph repository</exception>
    protected override CadmusGraphDbContext GetContext()
    {
        string? cs = ConnectionString;
        if (string.IsNullOrEmpty(cs))
        {
            throw new InvalidOperationException(
                "No connection string configured for graph repository");
        }

        // options are immutable and can be reused, which spares their
        // rebuilding at each operation
        lock (_optionsLock)
        {
            if (_options == null || _optionsConnectionString != cs)
            {
                DbContextOptionsBuilder<CadmusGraphDbContext> builder = new();
                builder.UseNpgsql(cs);
                _options = builder.Options;
                _optionsConnectionString = cs;
            }
            return new CadmusGraphDbContext(_options);
        }
    }

    /// <summary>
    /// Builds the regex match expression for field value vs pattern.
    /// </summary>
    /// <param name="field">The field name.</param>
    /// <param name="pattern">The regex pattern.</param>
    /// <returns>SQL code.</returns>
    protected override string BuildRegexMatch(string field, string pattern)
    {
        ArgumentNullException.ThrowIfNull(field);
        ArgumentNullException.ThrowIfNull(pattern);
        return $"{field} ~ '{pattern.Replace("'", "''")}'";
    }

    /// <summary>
    /// Acquires transaction-level advisory locks for the specified keys,
    /// in a consistent order.
    /// </summary>
    /// <param name="context">The context, with an active transaction.
    /// </param>
    /// <param name="space">The lock space.</param>
    /// <param name="keys">The keys to lock.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="InvalidOperationException">no transaction</exception>
    protected override async Task LockAsync(CadmusGraphDbContext context,
        int space, IEnumerable<string> keys, CancellationToken cancel)
    {
        string[] array = [.. keys.Select(k => k.ToLowerInvariant()).Distinct()];
        if (array.Length == 0) return;

        if (context.Database.CurrentTransaction == null)
        {
            throw new InvalidOperationException(
                "Graph locks require a transaction");
        }

        // lock in hash order, so that concurrent transactions locking
        // overlapping sets of keys cannot deadlock
        await context.Database.ExecuteSqlRawAsync(
            "SELECT pg_advisory_xact_lock({0}, s.h) FROM " +
            "(SELECT DISTINCT hashtext(k) AS h FROM unnest({1}) AS k " +
            "ORDER BY 1) AS s", [space, array], cancel);
    }

    /// <summary>
    /// Inserts the specified nodes unless they already exist, atomically.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="nodes">The nodes, sorted by ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    protected override async Task InsertMissingNodesAsync(
        CadmusGraphDbContext context, IList<Node> nodes,
        CancellationToken cancel)
    {
        if (nodes.Count == 0) return;

        List<Node> distinct = [.. nodes.DistinctBy(n => n.Id)];
        await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO node(id, is_class, tag, \"label\", source_type, sid) " +
            "SELECT * FROM unnest({0}::int4[], {1}::bool[], {2}::text[], " +
            "{3}::text[], {4}::int4[], {5}::text[]) " +
            "ON CONFLICT (id) DO NOTHING",
            [
                distinct.Select(n => n.Id).ToArray(),
                distinct.Select(n => n.IsClass).ToArray(),
                distinct.Select(n => Truncate(n.Tag, MAX_TAG)).ToArray(),
                distinct.Select(n => Truncate(n.Label, MAX_LABEL)).ToArray(),
                distinct.Select(n => n.SourceType).ToArray(),
                distinct.Select(n => Truncate(n.Sid, MAX_SID)).ToArray()
            ], cancel);
    }

    /// <summary>
    /// Creates the target store if it does not exist; else, upgrades its
    /// schema when required (see <see cref="PgSqlGraphSchema.ApplyAsync"/>).
    /// </summary>
    /// <param name="payload">Optional SQL code for seeding preset data
    /// in a new database.</param>
    /// <returns>
    /// True if created, false if already existing.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Missing connection string for PgSql graph repository, or
    /// Missing database name from connection string for PgSql graph repository.
    /// </exception>
    public bool CreateStore(object? payload = null)
    {
        // extract database name from connection string
        if (string.IsNullOrEmpty(ConnectionString))
        {
            throw new InvalidOperationException(
                "Missing connection string for PgSql graph repository");
        }
        Regex nameRegex = new("Database=([^;]+)", RegexOptions.IgnoreCase);
        Match m = nameRegex.Match(ConnectionString);
        if (!m.Success)
        {
            throw new InvalidOperationException(
                "Missing database name from connection string " +
                "for PgSql graph repository");
        }

        // create database if required
        PgSqlDbManager manager = new(
            nameRegex.Replace(ConnectionString, "Database={0}"));
        if (manager.Exists(m.Groups[1].Value))
        {
            PgSqlGraphSchema.ApplyAsync(ConnectionString)
                .GetAwaiter().GetResult();
            return false;
        }

        manager.CreateDatabase(m.Groups[1].Value, GetSchema(), payload as string);
        return true;
    }
}
