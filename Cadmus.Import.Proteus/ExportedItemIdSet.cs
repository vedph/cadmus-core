using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Import.Proteus;

/// <summary>
/// A set of exported item IDs, loaded from the output of
/// <see cref="ItemIdEntrySetExporter"/>. This is typically used in multi-pass
/// imports to resolve an <c>eid</c> into the IDs of the item and metadata
/// part it belongs to, but it also lets you browse all the exported data,
/// either in their original order or by item ID or <c>eid</c>.
/// </summary>
/// <remarks>Item IDs must be unique in the set. <c>eid</c> values should be
/// unique too, but duplicates are tolerated: <see cref="Get(string)"/> returns
/// the first item having the <c>eid</c>, <see cref="GetAll(string)"/> returns
/// all of them, and <see cref="GetDuplicateEids"/> lists all the duplicates
/// so that you can detect them.</remarks>
public sealed class ExportedItemIdSet : IReadOnlyCollection<ExportedItemId>
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    private readonly List<ExportedItemId> _items;
    private readonly Dictionary<string, ExportedItemId> _idMap;
    private readonly Dictionary<string, List<ExportedItemId>> _eidMap;

    /// <summary>
    /// Gets all the items in this set, in their original order.
    /// </summary>
    public IReadOnlyList<ExportedItemId> Items => _items;

    /// <summary>
    /// Gets the items keyed by their item ID.
    /// </summary>
    public IReadOnlyDictionary<string, ExportedItemId> ItemsById => _idMap;

    /// <summary>
    /// Gets all the distinct <c>eid</c> values in this set.
    /// </summary>
    public IReadOnlyCollection<string> Eids => _eidMap.Keys;

    /// <summary>
    /// Gets the comparer used for <c>eid</c> values.
    /// </summary>
    public IEqualityComparer<string> EidComparer => _eidMap.Comparer;

    /// <summary>
    /// Gets the count of items in this set.
    /// </summary>
    public int Count => _items.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportedItemIdSet"/> class
    /// from the specified items. You can use this to merge the items loaded
    /// from several exported files.
    /// </summary>
    /// <param name="items">The items.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c> values.
    /// The default is <see cref="StringComparer.Ordinal"/>, i.e. the same
    /// comparison used by the exporter.</param>
    /// <exception cref="ArgumentNullException">items</exception>
    /// <exception cref="InvalidDataException">Null item, item without ID,
    /// or duplicate item ID.</exception>
    public ExportedItemIdSet(IEnumerable<ExportedItemId> items,
        IEqualityComparer<string>? eidComparer = null)
    {
        ArgumentNullException.ThrowIfNull(items);

        _items = [];
        _idMap = new Dictionary<string, ExportedItemId>(StringComparer.Ordinal);
        _eidMap = new Dictionary<string, List<ExportedItemId>>(
            eidComparer ?? StringComparer.Ordinal);

        foreach (ExportedItemId? item in items)
        {
            if (item == null)
            {
                throw new InvalidDataException(
                    $"Null item at index {_items.Count}");
            }
            if (string.IsNullOrEmpty(item.Id))
            {
                throw new InvalidDataException(
                    $"Item without ID at index {_items.Count}");
            }
            if (!_idMap.TryAdd(item.Id, item))
                throw new InvalidDataException($"Duplicate item ID: {item.Id}");

            _items.Add(item);

            foreach (string? eid in item.Eids ?? [])
            {
                if (string.IsNullOrEmpty(eid)) continue;

                if (!_eidMap.TryGetValue(eid, out List<ExportedItemId>? list))
                {
                    list = [];
                    _eidMap[eid] = list;
                }
                // an item repeating the same eid is listed only once
                if (list.Count == 0 || !ReferenceEquals(list[^1], item))
                    list.Add(item);
            }
        }
    }

    #region Loading
    private static ExportedItemIdSet Create(List<ExportedItemId?>? items,
        IEqualityComparer<string>? eidComparer)
    {
        if (items == null)
            throw new InvalidDataException("Expected a JSON array of items");

        return new ExportedItemIdSet(items.Select(i => Normalize(i)!),
            eidComparer);
    }

    private static ExportedItemId? Normalize(ExportedItemId? item)
    {
        // JSON can contain nulls where the CLR model does not expect them
        if (item == null) return null;
        return new ExportedItemId
        {
            Id = item.Id,
            Title = item.Title ?? "",
            MetadataPartId = item.MetadataPartId,
            Eids = [.. (item.Eids ?? []).Where(e => !string.IsNullOrEmpty(e))],
        };
    }

    /// <summary>
    /// Parses the specified JSON text.
    /// </summary>
    /// <param name="json">The JSON text.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c>.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">json</exception>
    /// <exception cref="JsonException">Invalid JSON.</exception>
    /// <exception cref="InvalidDataException">Invalid data.</exception>
    public static ExportedItemIdSet Parse(string json,
        IEqualityComparer<string>? eidComparer = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        return Create(JsonSerializer.Deserialize<List<ExportedItemId?>>(
            json, _jsonOptions), eidComparer);
    }

    /// <summary>
    /// Loads the set from the specified UTF-8 JSON stream. The stream
    /// is not closed.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c>.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">stream</exception>
    /// <exception cref="JsonException">Invalid JSON.</exception>
    /// <exception cref="InvalidDataException">Invalid data.</exception>
    public static ExportedItemIdSet Load(Stream stream,
        IEqualityComparer<string>? eidComparer = null)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return Create(JsonSerializer.Deserialize<List<ExportedItemId?>>(
            stream, _jsonOptions), eidComparer);
    }

    /// <summary>
    /// Loads the set from the specified text reader. The reader is not closed.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c>.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">reader</exception>
    /// <exception cref="JsonException">Invalid JSON.</exception>
    /// <exception cref="InvalidDataException">Invalid data.</exception>
    public static ExportedItemIdSet Load(TextReader reader,
        IEqualityComparer<string>? eidComparer = null)
    {
        ArgumentNullException.ThrowIfNull(reader);

        return Parse(reader.ReadToEnd(), eidComparer);
    }

    /// <summary>
    /// Loads the set from the specified JSON file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c>.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">path</exception>
    /// <exception cref="JsonException">Invalid JSON.</exception>
    /// <exception cref="InvalidDataException">Invalid data.</exception>
    public static ExportedItemIdSet Load(string path,
        IEqualityComparer<string>? eidComparer = null)
    {
        ArgumentNullException.ThrowIfNull(path);

        using FileStream stream = File.OpenRead(path);
        return Load(stream, eidComparer);
    }

    /// <summary>
    /// Asynchronously loads the set from the specified UTF-8 JSON stream.
    /// The stream is not closed.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c>.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">stream</exception>
    /// <exception cref="JsonException">Invalid JSON.</exception>
    /// <exception cref="InvalidDataException">Invalid data.</exception>
    public static async Task<ExportedItemIdSet> LoadAsync(Stream stream,
        IEqualityComparer<string>? eidComparer = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        return Create(await JsonSerializer.DeserializeAsync<
            List<ExportedItemId?>>(stream, _jsonOptions, cancel),
            eidComparer);
    }

    /// <summary>
    /// Asynchronously loads the set from the specified JSON file.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="eidComparer">The optional comparer for <c>eid</c>.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The set.</returns>
    /// <exception cref="ArgumentNullException">path</exception>
    /// <exception cref="JsonException">Invalid JSON.</exception>
    /// <exception cref="InvalidDataException">Invalid data.</exception>
    public static async Task<ExportedItemIdSet> LoadAsync(string path,
        IEqualityComparer<string>? eidComparer = null,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(path);

        await using FileStream stream = new(path, FileMode.Open,
            FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        return await LoadAsync(stream, eidComparer, cancel);
    }
    #endregion

    #region Lookup
    /// <summary>
    /// Determines whether the specified <c>eid</c> is present in this set.
    /// </summary>
    /// <param name="eid">The eid.</param>
    /// <returns>True if present.</returns>
    /// <exception cref="ArgumentNullException">eid</exception>
    public bool ContainsEid(string eid)
    {
        ArgumentNullException.ThrowIfNull(eid);
        return _eidMap.ContainsKey(eid);
    }

    /// <summary>
    /// Gets the item having the specified <c>eid</c>. If more than one item
    /// has it, the first one is returned.
    /// </summary>
    /// <param name="eid">The eid.</param>
    /// <returns>The item, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">eid</exception>
    public ExportedItemId? Get(string eid)
    {
        ArgumentNullException.ThrowIfNull(eid);

        return _eidMap.TryGetValue(eid, out List<ExportedItemId>? list)
            ? list[0] : null;
    }

    /// <summary>
    /// Tries to get the item having the specified <c>eid</c>. If more than
    /// one item has it, the first one is returned.
    /// </summary>
    /// <param name="eid">The eid.</param>
    /// <param name="item">The item, or null if not found.</param>
    /// <returns>True if found.</returns>
    /// <exception cref="ArgumentNullException">eid</exception>
    public bool TryGet(string eid, [NotNullWhen(true)] out ExportedItemId? item)
    {
        item = Get(eid);
        return item != null;
    }

    /// <summary>
    /// Gets all the items having the specified <c>eid</c>. This usually is
    /// either empty or has a single item, unless <c>eid</c>s are duplicated.
    /// </summary>
    /// <param name="eid">The eid.</param>
    /// <returns>The items, in their original order.</returns>
    /// <exception cref="ArgumentNullException">eid</exception>
    public IReadOnlyList<ExportedItemId> GetAll(string eid)
    {
        ArgumentNullException.ThrowIfNull(eid);

        return _eidMap.TryGetValue(eid, out List<ExportedItemId>? list)
            ? list : [];
    }

    /// <summary>
    /// Gets the item with the specified item ID.
    /// </summary>
    /// <param name="id">The item ID.</param>
    /// <returns>The item, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public ExportedItemId? GetById(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _idMap.GetValueOrDefault(id);
    }

    /// <summary>
    /// Gets the <c>eid</c>s shared by more than one item, each with all the
    /// items having it.
    /// </summary>
    /// <returns>Duplicated eids with their items.</returns>
    public IEnumerable<KeyValuePair<string, IReadOnlyList<ExportedItemId>>>
        GetDuplicateEids()
    {
        foreach (KeyValuePair<string, List<ExportedItemId>> p in _eidMap)
        {
            if (p.Value.Count > 1)
            {
                yield return new KeyValuePair<string,
                    IReadOnlyList<ExportedItemId>>(p.Key, p.Value);
            }
        }
    }
    #endregion

    /// <summary>
    /// Gets an enumerator over the items, in their original order.
    /// </summary>
    /// <returns>Enumerator.</returns>
    public IEnumerator<ExportedItemId> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString() =>
        $"{nameof(ExportedItemIdSet)}: {_items.Count} items, " +
        $"{_eidMap.Count} eids";
}

/// <summary>
/// Item IDs exported by <see cref="ItemIdEntrySetExporter"/>.
/// </summary>
public sealed class ExportedItemId
{
    /// <summary>
    /// The item GUID.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The item's title.
    /// </summary>
    public string Title { get; init; } = "";

    /// <summary>
    /// The item's metadata part ID or null when the item has no metadata part.
    /// </summary>
    public string? MetadataPartId { get; init; }

    /// <summary>
    /// The <c>eid</c> values in the metadata part, if any.
    /// </summary>
    public IReadOnlyList<string> Eids { get; init; } = [];

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString() =>
        $"{Id}: {Title}" + (MetadataPartId != null
            ? $" [{MetadataPartId}: {string.Join(", ", Eids)}]" : "");
}
