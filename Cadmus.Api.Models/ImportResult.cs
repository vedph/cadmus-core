using System.Collections.Generic;

namespace Cadmus.Api.Models;

/// <summary>
/// Result of an import.
/// </summary>
public class ImportResult
{
    /// <summary>
    /// Gets or sets the IDs of the imported entries.
    /// </summary>
    public IList<string> ImportedIds { get; set; } = [];

    /// <summary>
    /// Gets or sets the error message if any.
    /// </summary>
    public string? Error { get; set; }
}
