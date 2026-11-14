using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Import.Excel;

/// <summary>
/// Converts selected bin ordinals into year intervals based on a configured
/// base year and bin size.
/// Bins are 1-based ordinals, and contiguous or adjacent selected bins are
/// merged into continuous year ranges.
/// </summary>
public sealed class DateBinsConverter
{
    /// <summary>
    /// The base year.
    /// </summary>
    public int BaseYear { get; set; } = -1000;

    /// <summary>
    /// The size of the bins, in years.
    /// </summary>
    public int BinSize { get; set; } = 25;

    private (int Start, int End) ComputeRange(int minOrdinal, int maxOrdinal)
    {
        int start = BaseYear + BinSize * (minOrdinal - 1);
        int end = BaseYear * 1 + BinSize * maxOrdinal - 1;
        return (start, end);
    }

    /// <summary>
    /// Converts an array of selected bin ordinals into a list of year range
    /// tuples.
    /// </summary>
    /// <param name="selectedBins">The array of selected bin ordinals.
    /// There can be a single sequence of consecutive bins (1, 1-3, 4-7, etc.)
    /// but also multiple non-consecutive sequences (1, 3-5, 9-10). Adjacent
    /// items in selectedBins must be merged (1-3 and 4 = 1-4). For each sequence,
    /// the resulting range will start with the bin with the minimum value and
    /// end with the bin with the maximum value.</param>
    /// <returns>A list of tuples representing the start and end years for each
    /// non-adjacent sequence of bins.</returns>
    /// <exception cref="ArgumentNullException">Thrown when
    /// <paramref name="selectedBins"/> is null.</exception>
    public IList<(int Start, int End)> Convert(List<int> selectedBins)
    {
        ArgumentNullException.ThrowIfNull(selectedBins);
        if (selectedBins.Count == 0) return [];

        int[] sortedBins = [.. selectedBins.Distinct().OrderBy(x => x)];
        List<(int Start, int End)> result = [];

        int rangeStart = sortedBins[0];
        int rangeEnd = sortedBins[0];

        for (int i = 1; i < sortedBins.Length; i++)
        {
            if (sortedBins[i] == rangeEnd + 1)
            {
                rangeEnd = sortedBins[i];
            }
            else
            {
                result.Add(ComputeRange(rangeStart, rangeEnd));
                rangeStart = sortedBins[i];
                rangeEnd = sortedBins[i];
            }
        }

        result.Add(ComputeRange(rangeStart, rangeEnd));
        return result;
    }
}
