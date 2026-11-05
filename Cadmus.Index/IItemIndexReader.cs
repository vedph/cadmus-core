using Cadmus.Core;
using Fusi.Tools.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index;

/// <summary>
/// Item index reader.
/// </summary>
public interface IItemIndexReader
{
    /// <summary>
    /// Searches for the items matching the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options. A page size equal to 0
    /// means no paging.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page of results, sorted by item sort key.</returns>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    Task<DataPage<ItemInfo>> SearchItemsAsync(string query,
        PagingOptions options, CancellationToken cancel = default);

    /// <summary>
    /// Searches for the pins matching the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options. A page size equal to 0
    /// means no paging.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page of results, sorted by pin name and value.</returns>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    Task<DataPage<DataPinInfo>> SearchPinsAsync(string query,
        PagingOptions options, CancellationToken cancel = default);

    #region Obsolete
    /// <summary>
    /// Searches for the items matching the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options.</param>
    /// <returns>Page of results.</returns>
    [Obsolete("Use SearchItemsAsync")]
    DataPage<ItemInfo> SearchItems(string query, PagingOptions options) =>
        SearchItemsAsync(query, options).GetAwaiter().GetResult();

    /// <summary>
    /// Searches for the pins matching the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options.</param>
    /// <returns>Page of results.</returns>
    [Obsolete("Use SearchPinsAsync")]
    DataPage<DataPinInfo> SearchPins(string query, PagingOptions options) =>
        SearchPinsAsync(query, options).GetAwaiter().GetResult();

    /// <summary>
    /// Formerly closed the connection to the target database. Connections
    /// are now pooled and released after each operation, so this does
    /// nothing.
    /// </summary>
    [Obsolete("No longer required: connections are released after each " +
        "operation")]
    void Close()
    {
    }
    #endregion
}
