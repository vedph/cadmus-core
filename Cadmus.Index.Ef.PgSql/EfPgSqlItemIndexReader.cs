using Cadmus.Index.PgSql;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Index.Ef.PgSql;

/// <summary>
/// DEPRECATED: compatibility alias for <see cref="PgSqlItemIndexReader"/>,
/// so that profiles using the legacy tag keep working. This no longer
/// uses Entity Framework.
/// <para>Tag: <c>item-index-reader.ef-pg</c>.</para>
/// </summary>
/// <seealso cref="PgSqlItemIndexReader" />
[Obsolete("Use PgSqlItemIndexReader (tag item-index-reader.pgsql) " +
    "from Cadmus.Index.PgSql")]
[Tag("item-index-reader.ef-pg")]
public sealed class EfPgSqlItemIndexReader : PgSqlItemIndexReader
{
}
