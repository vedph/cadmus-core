using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.Index.Config;
using Fusi.Tools;
using Fusi.Tools.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index;

/// <summary>
/// Items indexer. This uses an <see cref="IItemIndexWriter"/> to write
/// into the index the data pins from a single item or a set of items.
/// You can create an <see cref="IItemIndexWriter"/> using an
/// <see cref="ItemIndexFactory"/>, which requires a configuration
/// for the index writer, and a connection string.
/// The indexer can either add or update the indexes for a single item with its
/// parts, or build the whole index from all the items matching a specified
/// <see cref="ItemFilter"/>.
/// </summary>
public sealed class ItemIndexer
{
    /// <summary>
    /// The size of each page of items read from the repository and
    /// written to the index in a single batch.
    /// </summary>
    public const int PAGE_SIZE = 100;

    private readonly IItemIndexWriter _writer;

    /// <summary>
    /// Gets or sets the optional logger.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ItemIndexer"/> class.
    /// </summary>
    /// <param name="writer">The index writer.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public ItemIndexer(IItemIndexWriter writer)
    {
        _writer = writer
            ?? throw new ArgumentNullException(nameof(writer));
    }

    /// <summary>
    /// Adds or updates the specified item with its parts in the index.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">item</exception>
    public Task AddItemAsync(IItem item, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _writer.WriteItemAsync(item, cancel);
    }

    /// <summary>
    /// Clears the whole index.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    public Task ClearAsync(CancellationToken cancel = default) =>
        _writer.ClearAsync(cancel);

    private async Task WritePageAsync(List<IItem> items,
        CancellationToken cancel)
    {
        try
        {
            await _writer.WriteItemsAsync(items, null, cancel);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // the page was rolled back: retry item by item, so that only
            // the faulty items are skipped
            Logger?.LogWarning(ex, "Error indexing a page of {Count} items: " +
                "retrying one item at a time", items.Count);
            foreach (IItem item in items)
            {
                try
                {
                    await _writer.WriteItemAsync(item, cancel);
                }
                catch (Exception itemEx)
                    when (itemEx is not OperationCanceledException)
                {
                    Logger?.LogError(itemEx, "Error indexing item {ItemId}: " +
                        "{Title}", item.Id, item.Title);
                }
            }
        }
    }

    /// <summary>
    /// Builds the index by indexing all the items matching the specified
    /// filter in the specified repository. Each page of items is written in
    /// a single batch; if this fails, the page items are written one by one,
    /// logging and skipping those which fail.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="filter">The optional item filter; paging information
    /// will be overwritten.</param>
    /// <param name="progress">The optional progress reporter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The count of items read from the repository.</returns>
    /// <exception cref="ArgumentNullException">repository</exception>
    public async Task<int> BuildAsync(
        ICadmusRepository repository,
        ItemFilter? filter = null,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(repository);

        filter ??= new ItemFilter();
        filter.PageNumber = 1;
        filter.PageSize = PAGE_SIZE;

        DataPage<ItemInfo> page = repository.GetItems(filter);
        if (page.Total == 0) return 0;

        ProgressReport report = new();
        int count = 0;

        while (page.Items.Count > 0)
        {
            cancel.ThrowIfCancellationRequested();
            Logger?.LogInformation("Indexing page {PageNumber}/{PageCount}",
                page.PageNumber, page.PageCount);

            List<IItem> items = new(page.Items.Count);
            foreach (ItemInfo info in page.Items)
            {
                IItem? item = repository.GetItem(info.Id!);
                if (item == null)
                {
                    Logger?.LogWarning("Item {ItemId} not found", info.Id);
                    continue;
                }
                items.Add(item);
            }
            if (items.Count > 0) await WritePageAsync(items, cancel);

            count += page.Items.Count;
            if (progress != null)
            {
                report.Count = count;
                report.Percent = (int)(count * 100L / page.Total);
                progress.Report(report);
            }

            if (++filter.PageNumber > page.PageCount) break;
            page = repository.GetItems(filter);
        }
        return count;
    }

    #region Obsolete
    /// <summary>
    /// Adds the specified item to the index. This waits for completion
    /// before returning.
    /// </summary>
    /// <param name="item">The item.</param>
    [Obsolete("Use AddItemAsync")]
    public void AddItem(IItem item) =>
        AddItemAsync(item).GetAwaiter().GetResult();

    /// <summary>
    /// Clears the whole index.
    /// </summary>
    [Obsolete("Use ClearAsync")]
    public Task Clear() => ClearAsync();

    /// <summary>
    /// Builds the whole index. This waits for completion before returning.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <param name="filter">The optional item filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <param name="progress">The optional progress reporter.</param>
    [Obsolete("Use BuildAsync")]
    public void Build(
        ICadmusRepository repository,
        ItemFilter filter,
        CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        try
        {
            BuildAsync(repository, filter, progress, cancel)
                .GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            // the legacy method just stopped when cancelled
        }
    }
    #endregion
}
