using Cadmus.Index.PgSql;
using System;

// the namespace of the legacy Cadmus.Index.Ef package is preserved
namespace Cadmus.Index.Ef;

/// <summary>
/// DEPRECATED: compatibility alias for <see cref="PgSqlIndexOptions"/>.
/// </summary>
/// <seealso cref="PgSqlIndexOptions" />
[Obsolete("Use PgSqlIndexOptions from Cadmus.Index.PgSql")]
public class EfIndexRepositoryOptions : PgSqlIndexOptions
{
}
