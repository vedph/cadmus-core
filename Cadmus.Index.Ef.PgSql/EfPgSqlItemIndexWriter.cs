using Cadmus.Index.PgSql;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Index.Ef.PgSql;

/// <summary>
/// DEPRECATED: compatibility alias for <see cref="PgSqlItemIndexWriter"/>,
/// so that profiles using the legacy tag keep working. This no longer
/// uses Entity Framework.
/// <para>Tag: <c>item-index-writer.ef-pg</c>.</para>
/// </summary>
/// <seealso cref="PgSqlItemIndexWriter" />
[Obsolete("Use PgSqlItemIndexWriter (tag item-index-writer.pgsql) " +
    "from Cadmus.Index.PgSql")]
[Tag("item-index-writer.ef-pg")]
public sealed class EfPgSqlItemIndexWriter : PgSqlItemIndexWriter
{
    /// <summary>
    /// Gets the PostgreSQL schema used to populate a created database.
    /// </summary>
    /// <returns>SQL code.</returns>
    public static new string GetPgSqlSchema() => PgSqlIndexSchema.GetSql();
}
