using Fusi.Tools;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Proteus.Rendering;

/// <summary>
/// Default implementation of <see cref="IRendererContext"/>.
/// </summary>
public class RendererContext : DataDictionary, IRendererContext
{
    private readonly ConcurrentDictionary<string, int> _counters = [];

    /// <summary>
    /// Gets or sets the source.
    /// </summary>
    public object? Source { get; set; }

    /// <summary>
    /// Gets the identifier maps used in this context.
    /// </summary>
    public IDictionary<string, IdMap> IdMaps { get; } =
        new Dictionary<string, IdMap>();

    /// <summary>
    /// Clears this context.
    /// </summary>
    /// <param name="seeds">if set to <c>true</c> also reset the ID maps
    /// seeds.</param>
    public virtual void Clear(bool seeds = false)
    {
        Source = null;
        Data.Clear();

        foreach (IdMap map in IdMaps.Values) map.Reset(seeds);
        _counters.Clear();
    }

    /// <summary>
    /// Gets the next autonumber identifier for the category specified by
    /// <paramref name="categoryKey"/>. This is just a progressive number starting
    /// from 1.
    /// </summary>
    /// <param name="categoryKey">The key.</param>
    /// <returns>The next autonumber ID.</returns>
    /// <exception cref="ArgumentNullException">key</exception>
    public int GetNextIdFor(string categoryKey)
    {
        ArgumentNullException.ThrowIfNull(categoryKey);

        return _counters.AddOrUpdate(categoryKey, 1, (_, v) => v + 1);
    }

    /// <summary>
    /// Maps the specified source (e.g. fragment etc.) global identifier
    /// into a number. This is idempotent, i.e. if the ID has already been
    /// mapped, the same number is returned.
    /// </summary>
    /// <param name="map">The ID of the map to use.</param>
    /// <param name="id">The identifier to map.</param>
    /// <returns>Numeric ID.</returns>
    /// <exception cref="ArgumentNullException">map or id</exception>
    public int MapSourceId(string map, string id)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(id);

        if (!IdMaps.ContainsKey(map)) IdMaps[map] = new IdMap();
        return IdMaps[map].MapSourceId(id);
    }

    /// <summary>
    /// Gets the mapped ID for the specified source ID.
    /// </summary>
    /// <param name="map">The ID of the map to use.</param>
    /// <param name="id">The identifier to find the mapped ID of.</param>
    /// <returns>Numeric ID or null if not found.</returns>
    /// <exception cref="ArgumentNullException">map or id</exception>
    public int? GetMappedId(string map, string id)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(id);

        if (!IdMaps.TryGetValue(map, out IdMap? value)) return null;
        return value?.GetMappedId(id);
    }

    /// <summary>
    /// Gets the source identifier from its mapped unique number.
    /// </summary>
    /// <param name="map">The ID of the map to use.</param>
    /// <param name="id">The mapped number.</param>
    /// <returns>The source identifier or null if not found.</returns>
    /// <exception cref="ArgumentNullException">map</exception>
    public string? GetSourceId(string map, int id)
    {
        ArgumentNullException.ThrowIfNull(map);

        if (!IdMaps.ContainsKey(map)) IdMaps[map] = new IdMap();
        return IdMaps[map].GetSourceId(id);
    }
}
