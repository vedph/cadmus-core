using Fusi.Tools;
using Fusi.Tools.Data;
using Proteus.Core;

namespace Proteus.Rendering;

/// <summary>
/// Renderer of text trees.
/// </summary>
public interface ITextTreeRenderer<THandledType> : IHasLogger
{
    /// <summary>
    /// Resets the state of this renderer if any. This is called once before
    /// starting the rendering process.
    /// </summary>
    /// <param name="context">The context.</param>
    void Reset(IRendererContext context);

    /// <summary>
    /// Renders the head of the output. This is called by the item composer
    /// once when starting the rendering process and can be used to output
    /// specific content at the document's start.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Head content.</returns>
    THandledType? RenderHead(IRendererContext context);

    //void OnGroupChanged(IRendererContext context, string? prevGroupId);

    /// <summary>
    /// Renders the specified tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="context">The rendering context.</param>
    /// <returns>Rendition.</returns>
    THandledType? Render(TreeNode<ExportedSegment> tree, IRendererContext context);

    /// <summary>
    /// Renders the tail of the output. This is called by the item composer
    /// once when ending the rendering process and can be used to output
    /// specific content at the document's end.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Tail content.</returns>
    THandledType? RenderTail(IRendererContext context);
}
