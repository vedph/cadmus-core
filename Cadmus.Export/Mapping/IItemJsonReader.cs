using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cadmus.Export.Mapping;

/// <summary>
/// A reader for item's content in JSON format.
/// </summary>
public interface IItemJsonReader : IDisposable
{
    /// <summary>
    /// Reads the item with the specified identifier asynchronously.
    /// </summary>
    /// <param name="itemId">The identifier of the item to read.</param>
    /// <param name="filter">The optional filter for item parts, or null to
    /// read all parts.</param>
    /// <returns>The JSON representation of the item, or null if not found.</returns>
    Task<JsonDocument?> ReadAsync(string itemId, ItemPartFilter? filter);
}
