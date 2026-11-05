using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Fusi.Tools;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Index.Test;

/// <summary>
/// A synchronous progress reporter recording all the reports.
/// </summary>
internal sealed class RecordingProgress : IProgress<ProgressReport>
{
    public List<(int Count, int Percent)> Reports { get; } = [];

    public void Report(ProgressReport value) =>
        Reports.Add((value.Count, value.Percent));
}

/// <summary>
/// Fake writer recording the calls it receives.
/// </summary>
internal sealed class FakeItemIndexWriter : IItemIndexWriter
{
    public object? InitContext { get; set; }
    public List<string> Calls { get; } = [];
    public List<string> WrittenIds { get; } = [];
    public HashSet<string> FailingIds { get; } = [];
    public bool FailBatches { get; set; }

    public Task CreateIndexAsync(CancellationToken cancel = default)
    {
        Calls.Add("create");
        return Task.CompletedTask;
    }

    public Task WriteItemAsync(IItem item, CancellationToken cancel = default)
    {
        Calls.Add("write:" + item.Id);
        if (FailingIds.Contains(item.Id))
            throw new InvalidOperationException("Failing " + item.Id);
        WrittenIds.Add(item.Id);
        return Task.CompletedTask;
    }

    public Task WriteItemsAsync(IEnumerable<IItem> items,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancel = default)
    {
        List<IItem> list = [.. items];
        Calls.Add("writes:" + list.Count);
        if (FailBatches || list.Any(i => FailingIds.Contains(i.Id)))
            throw new InvalidOperationException("Failing batch");
        WrittenIds.AddRange(list.Select(i => i.Id));
        return Task.CompletedTask;
    }

    public Task DeleteItemAsync(string itemId, CancellationToken cancel = default)
    {
        Calls.Add("delete-item:" + itemId);
        return Task.CompletedTask;
    }

    public Task WritePartAsync(IItem item, IPart part,
        CancellationToken cancel = default)
    {
        Calls.Add("write-part:" + part.Id);
        return Task.CompletedTask;
    }

    public Task DeletePartAsync(string partId, CancellationToken cancel = default)
    {
        Calls.Add("delete-part:" + partId);
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancel = default)
    {
        Calls.Add("clear");
        return Task.CompletedTask;
    }
}

/// <summary>
/// Fake reader returning empty pages.
/// </summary>
internal sealed class FakeItemIndexReader : IItemIndexReader
{
    public List<string> Queries { get; } = [];

    public Task<DataPage<ItemInfo>> SearchItemsAsync(string query,
        PagingOptions options, CancellationToken cancel = default)
    {
        Queries.Add("items:" + query);
        return Task.FromResult(new DataPage<ItemInfo>(options.PageNumber,
            options.PageSize, 0, []));
    }

    public Task<DataPage<DataPinInfo>> SearchPinsAsync(string query,
        PagingOptions options, CancellationToken cancel = default)
    {
        Queries.Add("pins:" + query);
        return Task.FromResult(new DataPage<DataPinInfo>(options.PageNumber,
            options.PageSize, 0, []));
    }
}

/// <summary>
/// Fake repository with only <see cref="ICadmusRepository.GetItems"/> and
/// <see cref="ICadmusRepository.GetItem"/>, built as a dispatch proxy.
/// </summary>
public class FakeRepositoryProxy : DispatchProxy
{
    internal List<IItem> Items { get; set; } = [];
    internal HashSet<string> MissingIds { get; set; } = [];

    public static ICadmusRepository Create(IEnumerable<IItem> items,
        IEnumerable<string>? missingIds = null)
    {
        ICadmusRepository repository = Create<ICadmusRepository,
            FakeRepositoryProxy>();
        FakeRepositoryProxy proxy = (FakeRepositoryProxy)(object)repository;
        proxy.Items = [.. items];
        proxy.MissingIds = [.. missingIds ?? []];
        return repository;
    }

    protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
    {
        switch (targetMethod!.Name)
        {
            case nameof(ICadmusRepository.GetItems):
                ItemFilter filter = (ItemFilter)args![0]!;
                List<ItemInfo> page = Items
                    .Skip((filter.PageNumber - 1) * filter.PageSize)
                    .Take(filter.PageSize)
                    .Select(i => new ItemInfo { Id = i.Id, Title = i.Title })
                    .ToList();
                return new DataPage<ItemInfo>(filter.PageNumber,
                    filter.PageSize, Items.Count, page);
            case nameof(ICadmusRepository.GetItem):
                string id = (string)args![0]!;
                return MissingIds.Contains(id)
                    ? null : Items.FirstOrDefault(i => i.Id == id);
            default:
                throw new NotSupportedException(targetMethod.Name);
        }
    }
}

public sealed class TestIndexOptions
{
    public string? ConnectionString { get; set; }
    public IList<FlagDefinition>? FlagDefinitions { get; set; }
}

[Tag("test-index-reader")]
public sealed class TestConfigurableReader : IItemIndexReader,
    IConfigurable<TestIndexOptions>
{
    public TestIndexOptions? Options { get; private set; }

    public void Configure(TestIndexOptions options) => Options = options;

    public Task<DataPage<ItemInfo>> SearchItemsAsync(string query,
        PagingOptions options, CancellationToken cancel = default) =>
        throw new NotSupportedException();

    public Task<DataPage<DataPinInfo>> SearchPinsAsync(string query,
        PagingOptions options, CancellationToken cancel = default) =>
        throw new NotSupportedException();
}

[Tag("test-index-writer")]
public sealed class TestConfigurableWriter : IItemIndexWriter,
    IConfigurable<TestIndexOptions>
{
    public TestIndexOptions? Options { get; private set; }
    public object? InitContext { get; set; }

    public void Configure(TestIndexOptions options) => Options = options;

    public Task CreateIndexAsync(CancellationToken cancel = default) =>
        Task.CompletedTask;
    public Task WriteItemAsync(IItem item, CancellationToken cancel = default) =>
        Task.CompletedTask;
    public Task WriteItemsAsync(IEnumerable<IItem> items,
        IProgress<ProgressReport>? progress = null,
        CancellationToken cancel = default) => Task.CompletedTask;
    public Task DeleteItemAsync(string itemId,
        CancellationToken cancel = default) => Task.CompletedTask;
    public Task WritePartAsync(IItem item, IPart part,
        CancellationToken cancel = default) => Task.CompletedTask;
    public Task DeletePartAsync(string partId,
        CancellationToken cancel = default) => Task.CompletedTask;
    public Task ClearAsync(CancellationToken cancel = default) =>
        Task.CompletedTask;
}

/// <summary>
/// Test part with configurable pins.
/// </summary>
[Tag("it.vedph.test")]
internal sealed class TestPart : PartBase
{
    public List<DataPin> Pins { get; } = [];

    public override IEnumerable<DataPin> GetDataPins(IItem? item = null) =>
        Pins;

    public override IList<DataPinDefinition> GetDataPinDefinitions() => [];
}
