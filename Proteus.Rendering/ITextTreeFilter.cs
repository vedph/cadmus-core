using Fusi.Tools;
using Fusi.Tools.Data;
using Proteus.Core;

namespace Proteus.Rendering;

/// <summary>
/// A filter to be applied to a text tree.
/// </summary>
public interface ITextTreeFilter : IHasLogger
{
    /// <summary>
    /// Applies this filter to the specified tree, generating a new tree
    /// or just returning the received one.
    /// </summary>
    /// <param name="tree">The tree's root node.</param>
    /// <param name="source">The source being rendered.</param>
    /// <returns>The root node of the new tree.</returns>
    public TreeNode<ExportedSegment> Apply(TreeNode<ExportedSegment> tree,
        object? source = null);
}
