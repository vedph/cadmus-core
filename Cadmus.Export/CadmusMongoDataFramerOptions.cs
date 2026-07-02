namespace Cadmus.Export;

/// <summary>
/// Options for <see cref="CadmusMongoDataFramer"/>.
/// </summary>
public class CadmusMongoDataFramerOptions
{
    /// <summary>
    /// The MongoDB connection string template, having a <c>{0}</c> placeholder
    /// for the database name.
    /// </summary>
    public required string ConnectionString { get; set; } =
        "mongodb://localhost:27017/{0}";

    /// <summary>
    /// The name of the database to connect to.
    /// </summary>
    public required string DatabaseName { get; set; } = "cadmus";

    /// <summary>
    /// True to enable incremental framing mode. If true, only include
    /// items/parts changed (created, updated, or deleted) in the timeframe.
    /// If false, include all items/parts active or deleted as of the end of
    /// the timeframe.
    /// </summary>
    public bool IsIncremental { get; set; }

    /// <summary>
    /// True to not include parts' date in the export filters when time-based
    /// parameters are specified. When this is true, the export will consider
    /// only the modified time of the items, ignoring that of each item's parts.
    /// </summary>
    public bool NoPartDate { get; set; }

    /// <summary>
    /// True to not include deleted items in the export.
    /// </summary>
    public bool NoDeleted { get; set; }

    /// <summary>
    /// True to not include parts in the export. When this is false, parts
    /// are added to each non-deleted exported item in a <c>_parts</c> array
    /// property.
    /// </summary>
    public bool NoParts { get; set; }
}
