using Cadmus.Export.Renderers;
using Fusi.Tools.Data;
using Proteus.Rendering;

namespace Cadmus.Export;

/// <summary>
/// Base class for <see cref="IJsonRenderer"/>'s.
/// </summary>
public abstract class JsonRenderer : FilteredRenderer
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
        CadmusRendererContext? context = null,
        TreeNode<ExportedSegment>? tree = null);

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="json">The input JSON.</param>
    /// <param name="context">The optional renderer context.</param>
    /// <returns>Rendered output.</returns>
    /// <param name="tree">The optional text tree. This is used for layer
    /// fragments to get source IDs targeting the various portions of the
    /// text.</param>
    public string Render(string json, CadmusRendererContext context,
        TreeNode<ExportedSegment>? tree = null)
    {
        if (string.IsNullOrEmpty(json)) return json;

        object? result = DoRender(json, context, tree);
        return ApplyFilters(result, context);
    }
}
