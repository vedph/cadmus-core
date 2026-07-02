using Fusi.Tools.Data;

namespace Proteus.Rendering.Renderers;

/// <summary>
/// Base class for composite <see cref="ITextTreeRenderer{T}"/>'s. This
/// executes the inner renderer for each sub-tree in the received tree,
/// progressively composing the result into a single output.
/// </summary>
/// <seealso cref="IHasCompositeComponent{T}"/>
public abstract class CompositeTextTreeRenderer<THandledType> :
    TextTreeRenderer<THandledType>,
    IHasCompositeComponent<ITextTreeRenderer<THandledType>>
{
    /// <summary>
    /// Gets or sets the inner component.
    /// </summary>
    public ITextTreeRenderer<THandledType>? Component { get; set; }

    /// <summary>
    /// Called before rendering each sub-tree.
    /// </summary>
    /// <param name="tree">The multi-branches tree.</param>
    /// <param name="context">The renderer context.</param>
    /// <param name="index">The index of the sub-tree child of
    /// <paramref name="tree"/> to be rendered.</param>
    protected virtual void OnBeforeRendering(TreeNode<ExportedSegment> tree,
        IRendererContext context, int index)
    {
    }

    /// <summary>
    /// Composes the result for the specified sub-tree into <paramref name="target"/>.
    /// </summary>
    /// <param name="tree">The sub-tree being the source of the result.</param>
    /// <param name="context">The rendering context.</param>
    /// <param name="result">The sub-tree rendering result.</param>
    /// <param name="target">The compose target.</param>
    /// <returns>New or received compose target.</returns>
    protected abstract THandledType ComposeResult(TreeNode<ExportedSegment> tree,
        IRendererContext context, THandledType? result, THandledType? target);

    /// <summary>
    /// Renders the specified multi-tree.
    /// </summary>
    /// <param name="tree">The root node of the text tree.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Rendered output.</returns>
    protected override THandledType? DoRender(TreeNode<ExportedSegment> tree,
        IRendererContext context)
    {
        if (!tree.HasChildren || Component == null) return default;

        THandledType? target = default;
        int i = 0;
        foreach (TreeNode<ExportedSegment> sub in tree.Children)
        {
            OnBeforeRendering(tree, context, i++);
            THandledType? result = Component.Render(sub, context);
            target = ComposeResult(sub, context, result, target);
        }

        return target;
    }
}
