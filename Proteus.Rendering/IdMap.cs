using Fusi.Tools.Data;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Proteus.Rendering;

/// <summary>
/// A map of IDs between source and target. Sources are data like a fragment,
/// composed by a prefix (for the fragment, this is the layer part ID) and a
/// suffix (for the fragment, the fragment index). The target ID is just a
/// number. This class allows to map a source ID to a target ID, and vice versa.
/// </summary>
public class IdMap
{
    private readonly Trie _sourceMap = new();
    private readonly ConcurrentDictionary<int, string> _targetMap = new();
    private int _maxId;

    /// <summary>
    /// Gets the count of the entries in this map.
    /// </summary>
    public int Count => _targetMap.Count;

    /// <summary>
    /// Resets this map.
    /// </summary>
    /// <param name="seed">if set to <c>true</c>, also reset the seed.</param>
    public void Reset(bool seed = false)
    {
        if (seed) _maxId = 0;
        _sourceMap.Clear();
        _targetMap.Clear();
    }

    /// <summary>
    /// Maps the source identifier into a unique number.
    /// </summary>
    /// <param name="id">The source ID. This usually corresponds to a GUID
    /// prefix, e.g. a layer part ID for fragments, a text part ID for text segments,
    /// etc.; plus a suffix which is a scoped identifier in the context of the prefix,
    /// separated by an underscore, e.g. the fragment index in its layer part,
    /// or the node ID in a linear tree representing an item's base text.</param>
    /// <returns>The number.</returns>
    /// <exception cref="ArgumentNullException">prefix or suffix</exception>
    public int MapSourceId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        lock (_sourceMap)
        {
            TrieNode? node = _sourceMap.Get(id);
            if (node == null)
            {
                int newId = Interlocked.Increment(ref _maxId);
                _sourceMap.Add(id, newId);
                _targetMap[newId] = id;
                return newId;
            }

            return (int)node.Data!;
        }
    }

    /// <summary>
    /// Gets the mapped ID for the specified source ID.
    /// </summary>
    /// <param name="id">The identifier to find the mapped ID of.</param>
    /// <returns>Numeric ID or null if not found.</returns>
    /// <exception cref="ArgumentNullException">map or id</exception>
    public int? GetMappedId(string id)
    {
        ArgumentNullException.ThrowIfNull(id);
        return _sourceMap.Get(id)?.Data as int?;
    }

    /// <summary>
    /// Gets the source identifier from its mapped unique number.
    /// </summary>
    /// <param name="id">The mapped number.</param>
    /// <returns>The source identifier or null if not found.</returns>
    public string? GetSourceId(int id)
    {
        return _targetMap.TryGetValue(id, out string? key) ? key : null;
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return "IdMap: " + Count;
    }
}
