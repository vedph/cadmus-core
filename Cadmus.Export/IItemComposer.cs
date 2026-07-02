using Cadmus.Core;
using Microsoft.Extensions.Logging;
using Proteus.Rendering;
using System.Collections.Generic;
using System.IO;

namespace Cadmus.Export;

/// <summary>
/// Item composer interface. The item composer's task is composing some or
/// all the parts of an item together, building some specific output from it.
/// Its output is saved in an <see cref="ItemComposition"/>-derived object,
/// which in its base implementation is generic enough to fit most cases:
/// the output has a general name/value dictionary, and a set of
/// <see cref="TextWriter"/>'s, each identified by an arbitrary key.
/// <para>To use a composer, configure its components for the item's parts,
/// and call <see cref="Open(ItemComposition?)"/> when processing starts;
/// then call <see cref="Compose(IItem)"/> for each item to process as a unit,
/// and when done call <see cref="Close"/>. The output can either be passed
/// from outside the composer, or created internally.</para>
/// </summary>
public interface IItemComposer
{
    /// <summary>
    /// Gets or sets the renderer context suppliers.
    /// </summary>
    IList<ICadmusRendererContextSupplier> ContextSuppliers { get; set; }

    /// <summary>
    /// Gets or sets the optional text part flattener. This is used for text
    /// rendition when a part contains a base text and a set of layer parts.
    /// </summary>
    ITextPartFlattener? TextPartFlattener { get; set; }

    /// <summary>
    /// Gets or sets the optional text tree filters.
    /// </summary>
    IList<ITextTreeFilter> TextTreeFilters { get; set; }

    /// <summary>
    /// Gets or sets the optional text tree renderer.
    /// </summary>
    ICadmusTextTreeRenderer? TextTreeRenderer { get; set; }

    /// <summary>
    /// Gets the JSON renderers used by this composer under their keys.
    /// Each key is built from the part type ID, optionally followed by
    /// <c>:</c> and its role ID when present.
    /// </summary>
    IDictionary<string, IJsonRenderer> JsonRenderers { get; }

    /// <summary>
    /// Gets the ordinal item number. This is set to 0 when opening the
    /// composer, and increased whenever a new item is processed.
    /// </summary>
    int ItemNumber { get; }

    /// <summary>
    /// Gets the context using during processing.
    /// </summary>
    CadmusRendererContext Context { get; }

    /// <summary>
    /// Gets the output handled by this composer, or null if not opened.
    /// </summary>
    ItemComposition? Output { get; }

    /// <summary>
    /// Gets or sets the optional logger.
    /// </summary>
    ILogger? Logger { get; set; }

    /// <summary>
    /// Opens the composer output.
    /// </summary>
    /// <param name="output">The output object to use, or null to create
    /// a new one.</param>
    void Open(ItemComposition? output = null);

    /// <summary>
    /// Composes the output from the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    void Compose(IItem item);

    /// <summary>
    /// Closes the composer output.
    /// </summary>
    void Close();
}
