using Fusi.Tools.Data;
using Microsoft.Extensions.Logging;
using System;

namespace Proteus.Rendering;

/// <summary>
/// Base class for <see cref="ITextTreeRenderer{THandledType}"/> implementations.
/// </summary>
public abstract class TextTreeRenderer<THandledType> :
    ITextTreeRenderer<THandledType>
{
    /// <summary>
    /// Gets the type of the text object handled by this filter.
    /// </summary>
    public Type TextType => typeof(THandledType);

    /// <summary>
    /// Gets or sets the optional logger.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Resets the state of this renderer if any. This is called once before
    /// starting the rendering process.
    /// </summary>
    /// <param name="context">The context.</param>
    public virtual void Reset(IRendererContext context)
    {
        // nothing to do, override if required
    }

    /// <summary>
    /// Renders the head of the output. This is called by the item composer
    /// once when starting the rendering process and can be used to output
    /// specific content at the document's start.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Head content.</returns>
    public virtual THandledType? RenderHead(IRendererContext context)
    {
        return default;
    }

    /// <summary>
    /// Called when group has changed. The default implementation does
    /// nothing.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="prevGroupId">The previous group identifier.</param>
    public virtual void OnGroupChanged(IRendererContext context,
        string? prevGroupId)
    {
        // nothing to do, override if required
    }

    /// <summary>
    /// Renders the tail of the output. This is called by the item composer
    /// once when ending the rendering process and can be used to output
    /// specific content at the document's end.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Tail content.</returns>
    public virtual THandledType? RenderTail(IRendererContext context)
    {
        return default;
    }

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="tree">The root node of the text tree.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Rendered output.</returns>
    protected abstract THandledType? DoRender(TreeNode<ExportedSegment> tree,
        IRendererContext context);

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="tree">The root node of the text tree.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Rendered output.</returns>
    /// <exception cref="ArgumentNullException">tree</exception>
    public THandledType? Render(TreeNode<ExportedSegment> tree,
        IRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(tree);

        return DoRender(tree, context);
    }
}
