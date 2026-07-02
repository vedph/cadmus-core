using Cadmus.Core;
using Cadmus.Export.Renderers;
using Fusi.Tools.Data;
using Proteus.Core.Text;
using Proteus.Rendering;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// Base class for JSON renderers targeting markup languages. This adds
/// more specialized shared logic to <see cref="CadmusJsonRenderer"/>.
/// </summary>
/// <seealso cref="CadmusJsonRenderer" />
public abstract class MLJsonRenderer : CadmusJsonRenderer
{
    /// <summary>
    /// Finds the tree nodes representing the boundaries for the specified
    /// fragment, ensuring that only contiguous nodes are detected.
    /// </summary>
    /// <param name="prefix">The fragment prefix (typeId:roleId@FrIndex).</param>
    /// <param name="tree">The text tree root node.</param>
    /// <returns>Tuple with first and last node, which might be the same if
    /// the text spans for a single node.</returns>
    public static (TreeNode<ExportedSegment> First, TreeNode<ExportedSegment> Last)?
        FindFragmentBounds(string prefix, TreeNode<ExportedSegment> tree)
    {
        // find the first and last nodes having any fragment ID starting with prefix
        TreeNode<ExportedSegment>? firstNode = null;
        TreeNode<ExportedSegment>? lastNode = null;
        bool foundFirstNode = false;
        bool inFragmentSequence = false;

        tree.Traverse(node =>
        {
            AnnotatedTextRange? range =
                CadmusTextTreeBuilder.GetSegmentFirstRange(node.Data);
            if (range == null) return true;

            bool matchesPrefix = range.FragmentIds.Any(s => s.StartsWith(prefix));

            // if we found a node matching the prefix
            if (matchesPrefix)
            {
                // first matching node in the tree
                if (firstNode == null)
                {
                    firstNode = node;
                    lastNode = node;
                    foundFirstNode = true;
                    inFragmentSequence = true;
                }
                // subsequent matching node in a contiguous sequence
                else if (inFragmentSequence)
                {
                    lastNode = node;
                }
                // found a new matching node after the sequence was broken
                else if (foundFirstNode)
                {
                    // we already have a sequence and found a non-contiguous
                    // matching node; stop traversing since we only want the
                    // first contiguous sequence
                    return false;
                }
            }
            // if we found a node that doesn't match the prefix and we already 
            // had started collecting a sequence
            else if (foundFirstNode && inFragmentSequence)
            {
                // end of contiguous sequence
                inFragmentSequence = false;
            }

            return true;
        });

        if (firstNode != null && lastNode == null) return (firstNode, firstNode);

        return firstNode != null ? (firstNode, lastNode!) : null;
    }

    /// <summary>
    /// Adds the TEI location for a standoff notation to the specified target
    /// element. The location is either encoded as a single @loc attribute or
    /// as a loc child element with @spanFrom and @spanTo attributes.
    /// </summary>
    /// <param name="first">The first node linked to the fragment.</param>
    /// <param name="last">The last node linked to the fragment.</param>
    /// <param name="element">The target element to receive location.</param>
    /// <param name="context">The rendering context.</param>
    /// <exception cref="ArgumentNullException">any of the arguments is null
    /// </exception>
    public static void AddTeiLocToElement(TreeNode<ExportedSegment> first,
        TreeNode<ExportedSegment> last, XElement element,
        CadmusRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(last);
        ArgumentNullException.ThrowIfNull(element);
        ArgumentNullException.ThrowIfNull(context);

        IItem item = (IItem)context.Source!;
        if (first == last)
        {
            int id = context!.MapSourceId("seg", $"{item.Id}/{first}");
            element.SetAttributeValue("loc", $"seg{id}");
        }
        else
        {
            int firstId = context!.MapSourceId("seg", $"{item.Id}/{first}");
            int lastId = context!.MapSourceId("seg", $"{item.Id}/{last}");

            XElement loc = new(NamespaceOptions.TEI + "loc",
                new XAttribute("spanFrom", $"seg{firstId}"),
                new XAttribute("spanTo", $"seg{lastId}"));
            element.Add(loc);
        }
    }
}
