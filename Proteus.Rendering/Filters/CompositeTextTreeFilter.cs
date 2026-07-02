using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace Proteus.Rendering.Filters;

/// <summary>
/// A multiple trees text tree filter. This assumes that the received tree
/// has a blank root node whose children are each a separate tree. So, all the
/// children of the root will be blank nodes, each representing the sub-tree
/// root. The inner filter of this composite filter will be applied to each
/// such sub-tree.
/// <para>Tag: <c>it.vedph.text-tree-filter.composite</c>.</para>
/// </summary>
/// <seealso cref="ITextTreeFilter" />
[Tag("it.vedph.text-tree-filter.composite")]
public sealed class CompositeTextTreeFilter : ITextTreeFilter,
    IHasCompositeComponent<ITextTreeFilter>
{
    /// <summary>
    /// The subtree ID feature used for "multi"-trees. This is typically used
    /// with a value equal to the version tag corresponding to each subtree,
    /// and gets assigned to its blank root node.
    /// </summary>
    public const string F_SUBTREE_ID = "sub-id";

    /// <summary>
    /// Gets the inner component.
    /// </summary>
    public ITextTreeFilter? Component { get; set; }

    /// <summary>
    /// Gets or sets the logger.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Applies this filter to the specified tree, generating a new tree
    /// or just returning the received one.
    /// </summary>
    /// <param name="tree">The tree's root node.</param>
    /// <param name="source">The source being rendered.</param>
    /// <returns>
    /// The root node of the new tree.
    /// </returns>
    /// <exception cref="ArgumentNullException">tree</exception>
    public TreeNode<ExportedSegment> Apply(TreeNode<ExportedSegment> tree,
        object? source = null)
    {
        ArgumentNullException.ThrowIfNull(tree);

        // no subtrees or not configured, nothing to do
        if (tree.Children.Count == 0 || Component == null) return tree;

        // apply inner filter to each sub-tree
        foreach (TreeNode<ExportedSegment> sub in tree.Children.ToList())
        {
            TreeNode<ExportedSegment> filtered = Component.Apply(sub, source);
            if (filtered != sub) sub.ReplaceWith(filtered);
        }

        return tree;
    }
}
