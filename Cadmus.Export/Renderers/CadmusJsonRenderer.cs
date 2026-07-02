using System;
using Fusi.Tools.Data;
using Proteus.Rendering;

namespace Cadmus.Export.Renderers;

/// <summary>
/// Base class for <see cref="IJsonRenderer"/>'s.
/// </summary>
public abstract class CadmusJsonRenderer : FilteredRenderer
{
    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="json">The input JSON.</param>
    /// <param name="context">The optional renderer context.</param>
    /// <param name="tree">The optional text tree. This is used for layer
    /// fragments to get source IDs targeting the various portions of the
    /// text.</param>
    /// <returns>Rendered output.</returns>
    protected abstract string DoRender(string json,
        CadmusRendererContext context,
        TreeNode<ExportedSegment>? tree = null);

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="json">The input JSON.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Rendered output.</returns>
    /// <param name="tree">The optional text tree. This is used for layer
    /// fragments to get source IDs targeting the various portions of the
    /// text.</param>
    /// <exception cref="ArgumentNullException">json or context</exception>
    public string Render(string json, CadmusRendererContext context,
        TreeNode<ExportedSegment>? tree = null)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(context);

        object? result = DoRender(json, context, tree);
        return ApplyFilters(result, context);
    }
}
