using Cadmus.Core;
using Fusi.Tools.Data;
using Proteus.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Export;

/// <summary>
/// Cadmus text tree builder. This uses an <see cref="ITextPartFlattener"/>"
/// to build a linear tree of text segments from a text part and its layer parts.
/// Each segment gets a single payload of type <see cref="AnnotatedTextRange"/>
/// representing the source range of the text in the layer part and its
/// corresponding fragment IDs. Also, it has an
/// <see cref="ExportedSegment.F_EOL_TAIL"/> feature
/// with value=1 if the segment ended a line in the original text.
/// </summary>
public sealed class CadmusTextTreeBuilder
{
    private readonly ITextPartFlattener _flattener;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadmusTextTreeBuilder"/>.
    /// </summary>
    /// <param name="flattener">The part flattener to use.</param>
    /// <exception cref="ArgumentNullException">flattener</exception>
    public CadmusTextTreeBuilder(ITextPartFlattener flattener)
    {
        _flattener = flattener
            ?? throw new ArgumentNullException(nameof(flattener));
    }

    /// <summary>
    /// Gets the first Cadmus range from the specified text tree segment.
    /// </summary>
    /// <param name="segment">The segment or null.</param>
    /// <returns>The first range or null.</returns>
    public static AnnotatedTextRange? GetSegmentFirstRange(
        ExportedSegment? segment)
    {
        if (segment == null) return null;

        return segment.Payloads?.Count > 0
            ? (AnnotatedTextRange)segment.Payloads[0]
            : null;
    }

    /// <summary>
    /// Gets the first fragment ID matching the specified prefix from the
    /// payloads of the specified segment.
    /// </summary>
    /// <param name="segment">The segment or null.</param>
    /// <param name="prefix">The ID prefix to match.</param>
    /// <returns>The first matching fragment ID or null.</returns>
    public static string? GetFragmentIdWithPrefix(ExportedSegment? segment,
        string prefix)
    {
        if (segment == null ||
            segment.Payloads == null || segment.Payloads.Count == 0)
        {
            return null;
        }

        foreach (AnnotatedTextRange range in segment.Payloads
            .OfType<AnnotatedTextRange>())
        {
            // find the first fragment ID with the specified prefix
            string? frId = range.FragmentIds?.Find(
                id => id.StartsWith(prefix));
            if (frId != null) return frId;
        }

        return null;
    }

    /// <summary>
    /// Gets the fragment ID prefix used in text tree nodes to link fragments.
    /// This is a string like "it.vedph.token-text-layer:fr.it.vedph.comment@".
    /// </summary>
    /// <param name="layerPart">The layer part.</param>
    /// <returns>Prefix.</returns>
    public static string GetFragmentPrefixFor(IPart layerPart) =>
        $"{layerPart.TypeId}:{layerPart.RoleId}@";

    /// <summary>
    /// Gets the index of the fragment from its ID, with form
    /// <c>typeId:roleId@index</c> where index might be followed by a suffix.
    /// </summary>
    /// <param name="fragmentId">The fragment identifier.</param>
    /// <returns>Fragment index.</returns>
    /// <exception cref="ArgumentNullException">fragmentId</exception>
    /// <exception cref="FormatException">invalid fragment ID</exception>
    public static int GetFragmentIndex(string fragmentId)
    {
        ArgumentNullException.ThrowIfNull(fragmentId);
        int i = fragmentId.IndexOf('@');
        if (i < 0) throw new FormatException("Invalid fragment ID: " + fragmentId);
        int j = ++i;
        while (j < fragmentId.Length && char.IsDigit(fragmentId[j])) j++;
        return int.Parse(fragmentId[i..j]);
    }

    /// <summary>
    /// Gets the text part from the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The text part, or null if not found.</returns>
    private static IPart? GetTextPart(IItem item) => item.Parts.Find(
        p => p.RoleId == PartBase.BASE_TEXT_ROLE_ID);

    /// <summary>
    /// Gets the layer parts in the specified item, sorted by their role ID.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>The layer parts.</returns>
    private static IList<IPart> GetLayerParts(IItem item)
    {
        return [.. item.Parts
            .Where(p => p.RoleId?.StartsWith(PartBase.FR_PREFIX) == true)
            // just to ensure mapping consistency between successive runs
            .OrderBy(p => p.RoleId)];
    }

    /// <summary>
    /// Builds a tree from the received ranges. The tree has a blank root node,
    /// and each range is a child of the previous one.
    /// </summary>
    /// <param name="ranges">The ranges.</param>
    /// <param name="text">The whole text where lines are separated by a LF.
    /// </param>
    /// <returns>The root node of the built tree.</returns>
    /// <exception cref="ArgumentNullException">ranges</exception>
    public static TreeNode<ExportedSegment> BuildTreeFromRanges(
        IList<AnnotatedTextRange> ranges, string text)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        TreeNode<ExportedSegment> root = new();
        TreeNode<ExportedSegment> node = root;
        int n = 0;
        foreach (AnnotatedTextRange range in ranges)
        {
            // create a child node for this range
            TreeNode<ExportedSegment> child = new()
            {
                Id = $"{++n}",
                Label = DumpHelper.MapNonPrintables(range.Text),
                Data = new ExportedSegment(range.Text!, null, range)
            };

            // add features
            if (range.End + 1 < text.Length && text[range.End + 1] == '\n')
            {
                child.Data.AddFeature(ExportedSegment.F_EOL_TAIL, "\n");
            }

            node.AddChild(child);
            node = child;
        }
        return root;
    }

    /// <summary>
    /// Builds a text tree from the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="layerPartTypeIds">The layer part type IDs selected for
    /// rendering. If not set, all the layer parts will be selected.</param>
    /// <returns>Tree or null when the item has no text.</returns>
    public TreeNode<ExportedSegment>? Build(IItem item,
        HashSet<string>? layerPartTypeIds = null)
    {
        ArgumentNullException.ThrowIfNull(item);

        // text: there must be one
        IPart? textPart = GetTextPart(item);
        if (textPart == null) return null;

        // layers: collect item layer parts
        IList<IPart> layerParts = GetLayerParts(item);

        // filter out unwanted layer parts
        if (layerPartTypeIds?.Count > 0)
        {
            layerParts = [.. layerParts.Where(p =>
                layerPartTypeIds.Contains(p.TypeId))];
        }

        // flatten ranges
        Tuple<string, IList<AnnotatedTextRange>> tr =
            _flattener.Flatten(textPart, layerParts);

        // merge ranges
        IList<AnnotatedTextRange> mergedRanges = AnnotatedTextRange.GetConsecutiveRanges(
            0, tr.Item1.Length - 1, tr.Item2);

        // assign text to merged ranges
        foreach (AnnotatedTextRange range in mergedRanges)
            range.AssignText(tr.Item1);

        // build a linear tree from merged ranges
        return BuildTreeFromRanges(mergedRanges, tr.Item1);
    }
}
