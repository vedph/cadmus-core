using System.Collections.Generic;

namespace Cadmus.Export;

/// <summary>
/// Item ID collector interface. This defines the interface of any component
/// designed to collect a set of ordered item IDs from a Cadmus database,
/// in order to process them for preview or other export jobs.
/// </summary>
public interface IItemIdCollector
{
    /// <summary>
    /// Gets the items IDs.
    /// </summary>
    /// <returns>IDs.</returns>
    IEnumerable<string> GetIds();
}
