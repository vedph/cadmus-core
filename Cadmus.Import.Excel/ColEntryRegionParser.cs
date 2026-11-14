using Cadmus.Import.Proteus;
using Microsoft.Extensions.Logging;
using Proteus.Core.Entries;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Cadmus.Import.Excel;

/// <summary>
/// Base class for column entry region parsers. Each column region
/// is a sequence of column start command, text entry with the cell's value,
/// and column end command. This base class checks that there is a current
/// item, gets the filtered cell value, and passes it to
/// <see cref="ParseValue"/>. If the current row was skipped (see
/// <see cref="ImportHelper.SkipRow"/>), the column is ignored.
/// </summary>
/// <seealso cref="EntryRegionParser" />
public abstract class ColEntryRegionParser : EntryRegionParser
{
    /// <summary>
    /// Gets a value indicating whether the value passed to
    /// <see cref="ParseValue"/> must be lowercased.
    /// </summary>
    protected virtual bool LowercaseValue => false;

    /// <summary>
    /// Gets a value indicating whether the value passed to
    /// <see cref="ParseValue"/> must be just trimmed, rather than filtered.
    /// This preserves its line breaks and inner whitespaces.
    /// </summary>
    protected virtual bool RawValue => false;

    /// <summary>
    /// Parses the specified column value.
    /// </summary>
    /// <param name="context">The context, having a current item.</param>
    /// <param name="region">The column region.</param>
    /// <param name="value">The filtered value, or null if empty.</param>
    protected abstract void ParseValue(CadmusEntrySetContext context,
        EntryRegion region, string? value);

    /// <summary>
    /// Logs an error if <paramref name="value"/> is empty.
    /// </summary>
    /// <param name="region">The region.</param>
    /// <param name="value">The value.</param>
    /// <returns>True if the value is not empty.</returns>
    protected bool CheckRequired(EntryRegion region, string? value)
    {
        if (!string.IsNullOrEmpty(value)) return true;

        Logger?.LogError("{Tag} column without required value at region " +
            "{Region}", region.Tag, region);
        return false;
    }

    /// <summary>
    /// Parses the region of entries at <paramref name="entryRegionIndex" />
    /// in the specified <paramref name="entryRegions" />.
    /// </summary>
    /// <param name="entrySet">The entries set.</param>
    /// <param name="entryIndex">The index of the first entry to parse.</param>
    /// <param name="entryRegions">The regions.</param>
    /// <param name="entryRegionIndex">Index of the region in the set.</param>
    /// <returns>
    /// The index to the next entry to be parsed.
    /// </returns>
    /// <exception cref="ArgumentNullException">set or regions</exception>
    /// <exception cref="InvalidOperationException">no current item</exception>
    protected override Task<int> DoParseAsync(EntrySet entrySet, int entryIndex,
        IReadOnlyList<EntryRegion> entryRegions, int entryRegionIndex)
    {
        ArgumentNullException.ThrowIfNull(entrySet);
        ArgumentNullException.ThrowIfNull(entryRegions);

        CadmusEntrySetContext ctx = (CadmusEntrySetContext)entrySet.Context;
        EntryRegion region = entryRegions[entryRegionIndex];

        if (ctx.CurrentItem == null)
        {
            if (ImportHelper.IsRowSkipped(ctx))
                return Task.FromResult(entryIndex + 3);

            Logger?.LogError(
                "{Tag} column without any item at region {Region}",
                region.Tag, region);
            throw new InvalidOperationException(
                $"{region.Tag} column without any item at region {region}");
        }

        DecodedTextEntry? txt = entrySet.GetEntryAt<DecodedTextEntry>(
            entryIndex + 1);
        string? value = RawValue
            ? txt?.Value?.Trim()
            : ImportHelper.FilterValue(txt?.Value, LowercaseValue);

        ParseValue(ctx, region, string.IsNullOrEmpty(value) ? null : value);

        return Task.FromResult(entryIndex + 3);
    }
}

/// <summary>
/// Base options for configurable column parsers handling a single column.
/// </summary>
public class ColEntryRegionParserOptions : global::Proteus.Core.DisabledOptions
{
    /// <summary>
    /// Gets or sets the tag of the column region to be parsed, e.g.
    /// <c>col-site_id</c>.
    /// </summary>
    public string? Tag { get; set; }
}
