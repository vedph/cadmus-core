using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Proteus.Rendering;

namespace Cadmus.Export.Renderers;

/// <summary>
/// Null JSON renderer. This just returns the received JSON, and can be
/// used for diagnostic purposes, or to apply some filters to the received
/// text.
/// <para>Tag: <c>it.vedph.json-renderer.null</c>.</para>
/// </summary>
/// <seealso cref="IJsonRenderer" />
[Tag("it.vedph.json-renderer.null")]
public sealed class NullJsonRenderer : CadmusJsonRenderer, IJsonRenderer
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
    protected override string DoRender(string json,
        CadmusRendererContext context,
        TreeNode<ExportedSegment>? tree = null) => json ?? "";
}
