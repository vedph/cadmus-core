using Cadmus.Core;
using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index;

/// <summary>
/// Items index writer. The item index writer is used to add an item with
/// all its parts to the index, but also has the ability of creating its
/// target store if not present on the first write, and to remove all
/// data from it on demand.
/// </summary>
public interface IItemIndexWriter
{
    /// <summary>
    /// Gets or sets the optional initialization context data, which can be
    /// used when the writer has to create its target store.
    /// </summary>
    object? InitContext { get; set; }

    /// <summary>
    /// Creates the index if it does not exist, and ensures that its
    /// structure is up to date. If the index already exists and is up to
    /// date, nothing is done.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    Task CreateIndexAsync(CancellationToken cancel = default);

    /// <summary>
    /// Writes the specified item and all its parts (if any) to the index.
    /// The item is inserted or updated; the pins of each of its parts
    /// replace the pins previously indexed for the same part, while the
    /// pins of parts not included in the item are left untouched.
    /// If the index does not exist, it is created.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task WriteItemAsync(IItem item, CancellationToken cancel = default);

    /// <summary>
    /// Bulk-writes the specified items, with the same semantics as
    /// <see cref="WriteItemAsync"/> but with higher performance. This can
    /// be used to populate an index.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="progress">The optional progress reporter.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task WriteItemsAsync(IEnumerable<IItem> items,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancel = default);

    /// <summary>
    /// Deletes the item with the specified identifier with all its pins.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteItemAsync(string itemId, CancellationToken cancel = default);

    /// <summary>
    /// Writes the specified part to the index, replacing the pins previously
    /// indexed for it. Its item is inserted or updated too.
    /// </summary>
    /// <param name="item">The item the part belongs to.</param>
    /// <param name="part">The part.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task WritePartAsync(IItem item, IPart part,
        CancellationToken cancel = default);

    /// <summary>
    /// Deletes the part with the specified ID from the index.
    /// </summary>
    /// <param name="partId">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeletePartAsync(string partId, CancellationToken cancel = default);

    /// <summary>
    /// Clears the whole index.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    Task ClearAsync(CancellationToken cancel = default);

    #region Obsolete
    /// <summary>
    /// Creates the index. This waits for completion before returning.
    /// </summary>
    [Obsolete("Use CreateIndexAsync")]
    Task CreateIndex()
    {
        CreateIndexAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes the specified item. This waits for completion before
    /// returning.
    /// </summary>
    /// <param name="item">The item.</param>
    [Obsolete("Use WriteItemAsync")]
    Task WriteItem(IItem item)
    {
        WriteItemAsync(item).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Bulk-writes the specified items. This waits for completion before
    /// returning.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <param name="progress">The optional progress reporter.</param>
    [Obsolete("Use WriteItemsAsync")]
    Task WriteItems(IEnumerable<IItem> items, CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        WriteItemsAsync(items, progress, cancel).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the specified item. This waits for completion before
    /// returning.
    /// </summary>
    /// <param name="itemId">The item identifier.</param>
    [Obsolete("Use DeleteItemAsync")]
    Task DeleteItem(string itemId)
    {
        DeleteItemAsync(itemId).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes the specified part. This waits for completion before
    /// returning.
    /// </summary>
    /// <param name="item">The item the part belongs to.</param>
    /// <param name="part">The part.</param>
    [Obsolete("Use WritePartAsync")]
    Task WritePart(IItem item, IPart part)
    {
        WritePartAsync(item, part).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the specified part. This waits for completion before
    /// returning.
    /// </summary>
    /// <param name="partId">The part identifier.</param>
    [Obsolete("Use DeletePartAsync")]
    Task DeletePart(string partId)
    {
        DeletePartAsync(partId).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clears the whole index. This waits for completion before returning.
    /// </summary>
    [Obsolete("Use ClearAsync")]
    Task Clear()
    {
        ClearAsync().GetAwaiter().GetResult();
        return Task.CompletedTask;
    }

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
