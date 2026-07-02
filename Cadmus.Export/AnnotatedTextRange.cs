using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Export;

/// <summary>
/// A text range linked to one or more fragments.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AnnotatedTextRange"/>
/// class.
/// </remarks>
/// <param name="start">The start.</param>
/// <param name="end">The end.</param>
/// <param name="frIds">The optional fragment identifier(s).</param>
public class AnnotatedTextRange(int start, int end, params IList<string>? frIds)
{
    /// <summary>
    /// Gets or sets the start index.
    /// </summary>
    public int Start { get; set; } = start;

    /// <summary>
    /// Gets or sets the end index.
    /// </summary>
    public int End { get; set; } = end;

    /// <summary>
    /// Gets or sets the identifiers of the fragments attached to this range.
    /// </summary>
    public List<string> FragmentIds { get; } = frIds?.ToList() ?? [];

    /// <summary>
    /// Gets or sets the text corresponding to this range.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// True if this range contains the specified index.
    /// </summary>
    /// <param name="index">Index.</param>
    /// <returns>True if index is contained.</returns>
    public bool Contains(int index) => index >= Start && index <= End;

    /// <summary>
    /// Assigns the text to this range according to the specified text.
    /// </summary>
    /// <param name="text">The source text.</param>
    /// <exception cref="ArgumentNullException">text</exception>
    public void AssignText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        Text = text.Substring(Start, End - Start + 1);
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{Start}-{End} \"{Text}\" {string.Join(", ", FragmentIds)}";
    }

    /// <summary>
    /// Gets a sorted list of consecutive, adjacent ranges starting from the
    /// specified sparse ranges within the specified boundaries. Each range
    /// is linked to zero or more fragment IDs. When two ranges are separated
    /// by a gap, a new range will be created, linked to no fragment IDs, to
    /// fill it.
    /// </summary>
    /// <param name="start">The start text index.</param>
    /// <param name="end">The end text index.</param>
    /// <param name="ranges">The ranges to merge.</param>
    /// <returns>Merged ranges.</returns>
    /// <exception cref="ArgumentNullException">ranges</exception>
    /// <exception cref="ArgumentException">Start must not be greater than end
    /// </exception>
    public static IList<AnnotatedTextRange> GetConsecutiveRanges(
        int start, int end, IList<AnnotatedTextRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);
        if (start > end)
            throw new ArgumentException("Start must not be greater than end");

        List<AnnotatedTextRange> result = [];

        // if no ranges, create a single range with no fragments
        if (ranges.Count == 0)
        {
            if (start <= end) result.Add(new AnnotatedTextRange(start, end));
            return result;
        }

        // create a sorted list of all unique positions where fragment
        // combinations change
        SortedSet<int> positions = [start, end + 1];
        foreach (AnnotatedTextRange range in ranges)
        {
            positions.Add(range.Start);
            positions.Add(range.End + 1);
        }

        // convert to array for easier sequential access
        int[] posArray = [.. positions];

        // for each segment between positions, create a new range if needed
        for (int i = 0; i < posArray.Length - 1; i++)
        {
            int currentStart = posArray[i];
            int currentEnd = posArray[i + 1] - 1;

            // skip if outside requested range
            if (currentEnd < start || currentStart > end) continue;

            // adjust boundaries to requested range
            currentStart = Math.Max(currentStart, start);
            currentEnd = Math.Min(currentEnd, end);

            // find all fragments that cover this range
            HashSet<string> fragmentIds = [];
            foreach (AnnotatedTextRange range in ranges)
            {
                if (range.Start <= currentEnd && range.End >= currentStart)
                    fragmentIds.UnionWith(range.FragmentIds);
            }

            // create new range with combined fragments
            AnnotatedTextRange newRange = new(currentStart, currentEnd);
            newRange.FragmentIds.AddRange(fragmentIds.Order());
            result.Add(newRange);
        }

        return result;
    }
}
