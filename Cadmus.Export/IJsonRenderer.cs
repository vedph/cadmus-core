using Fusi.Tools.Data;
using Proteus.Core.Text;
using Proteus.Rendering;
using System.Collections.Generic;

namespace Cadmus.Export;

/// <summary>
/// Renderer for any object represented by JSON (like a part or a fragment).
/// This takes as input the JSON code, and renders it into some output format.
/// </summary>
public interface IJsonRenderer
{
    /// <summary>
    /// The filters.
    /// </summary>
    List<ITextFilter> Filters { get; }

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="json">The input JSON.</param>
    /// <param name="context">The renderer context.</param>
    /// <param name="tree">The optional text tree. This is used for layer
    /// fragments to get source IDs targeting the various portions of the
    /// text.</param>
    /// <returns>Rendered output.</returns>
    string Render(string json, CadmusRendererContext context,
        TreeNode<ExportedSegment>? tree = null);
}
