using Cadmus.Core.Config;
using System.Collections.Generic;

namespace Cadmus.Index.PgSql;

/// <summary>
/// Options for the PostgreSQL index reader and writer.
/// </summary>
public class PgSqlIndexOptions
{
    /// <summary>
    /// Gets or sets the connection string. When using an
    /// <see cref="Config.ItemIndexFactory"/>, this is supplied by the
    /// factory if not specified.
    /// </summary>
    public string ConnectionString { get; set; } = "";

    /// <summary>
    /// Gets or sets the flag definitions, used by the reader to resolve
    /// flag labels in queries. When using an
    /// <see cref="Config.ItemIndexFactory"/>, these are supplied by the
    /// factory from the profile if not specified.
    /// </summary>
    public IList<FlagDefinition>? FlagDefinitions { get; set; }

    /// <summary>
    /// Gets or sets the timeout in seconds for each database command.
    /// The default is 30. Use 0 for no timeout.
    /// </summary>
    public int CommandTimeout { get; set; } = 30;
}
