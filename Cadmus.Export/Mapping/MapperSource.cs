using Cadmus.Core;
using System;

namespace Cadmus.Export.Mapping;

/// <summary>
/// Source data for Cadmus objects mapping. This can be either an item or an
/// item's part.
/// </summary>
public class MapperSource
{
    /// <summary>
    /// Gets the item.
    /// </summary>
    public IItem Item { get; }

    /// <summary>
    /// Gets the part.
    /// </summary>
    public IPart? Part { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapperSource"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException">item</exception>
    public MapperSource(IItem item)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MapperSource"/> class.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="part">The part.</param>
    /// <exception cref="ArgumentNullException">item or part</exception>
    public MapperSource(IItem item, IPart part)
    {
        Item = item ?? throw new ArgumentNullException(nameof(item));
        Part = part ?? throw new ArgumentNullException(nameof(part));
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        if (Part != null) return Part.ToString()!;
        return Item!.ToString()!;
    }
}
