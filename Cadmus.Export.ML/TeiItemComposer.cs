using Cadmus.Core;
using Fusi.Tools.Data;
using Proteus.Rendering;

namespace Cadmus.Export.ML;

/// <summary>
/// Simple TEI item composer.
/// </summary>
/// <seealso cref="ItemComposer" />
public abstract class TeiItemComposer : ItemComposer
{
    /// <summary>
    /// The text flow metadata key (<c>flow-key</c>).
    /// </summary>
    public const string M_FLOW_KEY = "flow-key";

    /// <summary>
    /// The layer identifier (<c>layer-id</c>). This is from the renderer
    /// context layer ID mappings.
    /// </summary>
    public const string M_LAYER_ID = "layer-id";

    /// <summary>
    /// The prefix to add to item identifier values in <c>source</c> attributes.
    /// </summary>
    public const string ITEM_ID_PREFIX = "^";

    /// <summary>
    /// Composes the output from the specified item.
    /// </summary>
    /// <returns>Composition result or null.</returns>
    protected override void DoCompose()
    {
        if (Output == null || TextTreeRenderer == null || Context.Source == null)
            return;

        // build text tree
        TreeNode<ExportedSegment>? tree = BuildTextTree((IItem)Context.Source);
        if (tree == null) return;

        // render text from tree
        string? result = TextTreeRenderer.Render(tree, Context);
        if (!string.IsNullOrEmpty(result))
            WriteOutput(PartBase.BASE_TEXT_ROLE_ID, result);
    }
}
