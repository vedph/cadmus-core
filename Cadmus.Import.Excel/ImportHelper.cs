using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using Microsoft.Extensions.Logging;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Import.Excel;

/// <summary>
/// Helper functions for Excel-based import parsers.
/// </summary>
public static partial class ImportHelper
{
    // values representing an empty cell
    private static readonly HashSet<string> _emptyValues =
        new(StringComparer.OrdinalIgnoreCase) { "n/a" };

    [GeneratedRegex(@"\s+")]
    private static partial Regex WsRegex();

    /// <summary>
    /// The key used to get the item IDs set loaded in the pipeline data context
    /// when present. This is used to lookup target items to link to. When
    /// several item ID files are loaded, this is the merge of all of them,
    /// while each of them is also available under its own key (see
    /// <see cref="GetItemIdsKey(string?)"/>).
    /// </summary>
    public const string ITEM_IDS_KEY = "*item-ids";

    /// <summary>
    /// The key of the context data flag set when the current row must be
    /// skipped. This is not global, so it is automatically removed when the
    /// context is reset for the next row.
    /// </summary>
    public const string SKIP_ROW_KEY = "skip-row";

    /// <summary>
    /// Gets the context data key for the item IDs set with the specified name.
    /// </summary>
    /// <param name="name">The set name, i.e. the name of the file it was
    /// loaded from without its extension (e.g. <c>item-ids-s</c>), or null
    /// to get the key of the merged set.</param>
    /// <returns>Key.</returns>
    public static string GetItemIdsKey(string? name) =>
        string.IsNullOrEmpty(name) ? ITEM_IDS_KEY : ITEM_IDS_KEY + ":" + name;

    /// <summary>
    /// Filters a cell value. This trims it, normalizes whitespaces to a single
    /// space, and optionally lowercases it. If the resulting value represents
    /// an empty value (e.g. <c>N/A</c>), null is returned.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="lowercase">True to lowercase the result.</param>
    /// <returns>Filtered value.</returns>
    public static string? FilterValue(string? value, bool lowercase)
    {
        if (string.IsNullOrEmpty(value)) return value;

        // trim
        value = value.Trim();

        // normalize whitespaces to single space
        value = WsRegex().Replace(value, " ");

        // lowercase if required
        if (lowercase) value = value.ToLowerInvariant();

        return _emptyValues.Contains(value) ? null : value;
    }

    /// <summary>
    /// Gets a list of comma-separated values from the specified text value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="lowercase">True to lowercase the result.</param>
    /// <param name="separators">The optional separators to use. If not specified,
    /// comma will be the default.</param>
    /// <returns>List.</returns>
    public static IList<string> GetValueList(string? value, bool lowercase,
        char[]? separators = null)
    {
        if (string.IsNullOrEmpty(value)) return Array.Empty<string>();

        separators ??= [','];
        return [.. (from v in value.Split(separators,
                    StringSplitOptions.RemoveEmptyEntries)
                select FilterValue(v, lowercase) into v
                where !string.IsNullOrEmpty(v)
                select v)];
    }

    /// <summary>
    /// Gets the nullable boolean value corresponding to the specified cell
    /// string value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Nullable boolean.</returns>
    public static bool? GetNullableBooleanValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;
        value = FilterValue(value, true);
        return value switch
        {
            "si" => true,
            "no" => false,
            _ => null
        };
    }

    /// <summary>
    /// Gets the boolean value corresponding to the specified cell
    /// string value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Boolean.</returns>
    public static bool GetBooleanValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        value = FilterValue(value, true);
        return value switch
        {
            "si" => true,
            "no" => false,
            _ => false
        };
    }

    /// <summary>
    /// Gets the int value corresponding to the specified cell string value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Integer.</returns>
    public static int GetIntValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        value = FilterValue(value, true);
        return int.TryParse(value, out int n) ? n : 0;
    }

    /// <summary>
    /// Gets the date value with format DD/MM/YYYY.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Value or null if empty or invalid.</returns>
    public static DateOnly? GetDateValue(string? value)
    {
        if (string.IsNullOrEmpty(value)) return null;

        value = FilterValue(value, true);
        if (string.IsNullOrEmpty(value)) return null;

        // parse from formats like D/MM/YYYY, DD/M/YYYY, etc.
        // and also tolerate \ for /
        string[] dateParts = value.Replace('\\', '/').Split('/');
        if (dateParts.Length == 3 &&
            int.TryParse(dateParts[0], out int day) &&
            int.TryParse(dateParts[1], out int month) &&
            int.TryParse(dateParts[2], out int year))
        {
            return new DateOnly(year, month, day);
        }

        return null;
    }

    /// <summary>
    /// Returns true if the specified cell value is <c>YES</c> (case-insensitive).
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True if yes.</returns>
    public static bool IsYes(string? value) =>
        string.Equals(value?.Trim(), "yes", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Tries to parse a decimal number like a coordinate. Cell values may come
    /// formatted according to the current culture, so both comma and dot are
    /// accepted as decimal separators. Thousands separators are not allowed.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <param name="result">The result.</param>
    /// <returns>True if parsed.</returns>
    public static bool TryParseDecimal(string? value, out double result)
    {
        result = 0;
        if (string.IsNullOrWhiteSpace(value)) return false;

        return double.TryParse(value.Trim().Replace(',', '.'),
            NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out result);
    }

    /// <summary>
    /// Gets the thesaurus identifier for the specified entry value in the
    /// specified thesaurus. Values are matched exactly first, and then
    /// case-insensitively. In case the ID is not found, an error is logged
    /// if <paramref name="logger"/> is not null, and the value itself is
    /// returned, so that the data is not lost.
    /// </summary>
    /// <param name="context">The data context.</param>
    /// <param name="region">The current region.</param>
    /// <param name="thesaurusId">The thesaurus identifier.</param>
    /// <param name="value">The entry value.</param>
    /// <param name="logger">The optional logger.</param>
    /// <returns>ID or value if not found.</returns>
    public static string GetThesaurusEntryId(CadmusEntrySetContext context,
        EntryRegion region, string thesaurusId, string value,
        ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(region);
        ArgumentNullException.ThrowIfNull(thesaurusId);
        ArgumentNullException.ThrowIfNull(value);

        if (context.ThesaurusEntryMap == null)
        {
            logger?.LogError("No thesaurus map available for {Tag} " +
                "at region {Region}", region.Tag, region);
            return value;
        }

        // adjust value: _ -> space, \ -> /
        value = value.Replace('_', ' ').Replace('\\', '/');

        string? id = context.ThesaurusEntryMap.GetEntryId(thesaurusId, value);

        if (id == null)
        {
            Thesaurus? thesaurus =
                context.ThesaurusEntryMap.GetThesaurus(thesaurusId);
            if (thesaurus == null)
            {
                logger?.LogError("Unknown thesaurus {ThesaurusId} for {Tag} " +
                    "at region {Region}", thesaurusId, region.Tag, region);
                return value;
            }

            id = thesaurus.Entries.FirstOrDefault(e => string.Equals(
                e.Value, value, StringComparison.OrdinalIgnoreCase))?.Id;
        }

        if (id == null)
        {
            logger?.LogError("Unknown value for {Tag} in {ThesaurusId}: " +
                "\"{Value}\" at region {Region}",
                region.Tag, thesaurusId, value, region);
            id = value;
        }

        return id;
    }

    /// <summary>
    /// Returns true when the ID of the specified thesaurus entry is valid,
    /// false otherwise.
    /// </summary>
    /// <param name="context">The data context.</param>
    /// <param name="thesaurusId">The thesaurus identifier.</param>
    /// <param name="entryId">The entry ID.</param>
    /// <param name="logger">The optional logger.</param>
    /// <returns>True if the entry ID is valid, false otherwise.</returns>
    public static bool ValidateThesaurusEntryId(CadmusEntrySetContext context,
        string thesaurusId, string entryId, ILogger? logger)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(thesaurusId);
        ArgumentNullException.ThrowIfNull(entryId);

        // get thesaurus
        Thesaurus? thesaurus = context.ThesaurusEntryMap?.GetThesaurus(thesaurusId);
        if (thesaurus == null)
        {
            logger?.LogError("Unknown thesaurus {ThesaurusId} for entry " +
                "\"{EntryId}\"", thesaurusId, entryId);
            return false;
        }

        // check entry
        bool valid = thesaurus.Entries.Any(e => e.Id == entryId);
        if (!valid)
        {
            logger?.LogError("Unknown entry ID for thesaurus {ThesaurusId}: " +
                "\"{EntryId}\"", thesaurusId, entryId);
        }

        return valid;
    }

    /// <summary>
    /// Converts a label string to a 16-bit rank value.
    /// </summary>
    /// <remarks>The input is normalized via FilterValue(true) before matching.
    /// </remarks>
    /// <param name="value">Label text to parse; null or whitespace yields 0.
    /// The input is filtered before parsing.</param>
    /// <param name="logger">The optional logger.</param>
    /// <returns>A 16-bit integer parsed from the filtered label, or 0 if the
    /// input is null, whitespace, or not a valid number.</returns>
    public static short LabelToRank(string? value, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        value = FilterValue(value, true);
        switch (value)
        {
            case "high": return 1;
            case "medium": return 2;
            case "low": return 3;
            default:
                logger?.LogError("Unable to parse rank from label: \"{Value}\"",
                    value);
                break;
        }

        return 0;
    }

    /// <summary>
    /// Sets the rank of the specified assertion owner, creating its assertion
    /// if required.
    /// </summary>
    /// <param name="target">The target.</param>
    /// <param name="rank">The rank.</param>
    public static void SetRank(IHasAssertion target, short rank)
    {
        ArgumentNullException.ThrowIfNull(target);

        target.Assertion ??= new Assertion();
        target.Assertion.Rank = rank;
    }

    /// <summary>
    /// Appends the specified text to a note, optionally under a Markdown
    /// heading. Different texts are separated by a blank line.
    /// </summary>
    /// <param name="note">The note to append to, or null.</param>
    /// <param name="text">The text to append.</param>
    /// <param name="heading">The optional heading.</param>
    /// <returns>The resulting note.</returns>
    public static string AppendToNote(string? note, string text,
        string? heading = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        StringBuilder sb = new(note ?? "");
        if (sb.Length > 0) sb.Append("\n\n");
        if (!string.IsNullOrEmpty(heading))
            sb.Append("### ").Append(heading).Append("\n\n");
        sb.Append(text);
        return sb.ToString();
    }

    /// <summary>
    /// Marks the current row as skipped, removing its item from the context
    /// so that nothing gets exported for it. Column parsers derived from
    /// <see cref="ColEntryRegionParser"/> ignore all the columns of a
    /// skipped row.
    /// </summary>
    /// <param name="context">The context.</param>
    public static void SkipRow(CadmusEntrySetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Items.Clear();
        context.Data[SKIP_ROW_KEY] = true;
    }

    /// <summary>
    /// Determines whether the current row was marked as skipped.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>True if skipped.</returns>
    public static bool IsRowSkipped(CadmusEntrySetContext context) =>
        context.Data.ContainsKey(SKIP_ROW_KEY);

    /// <summary>
    /// Changes the ID of the specified item, updating the item ID of all
    /// its parts accordingly.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="id">The new ID.</param>
    public static void SetItemId(IItem item, string id)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(id);

        item.Id = id;
        foreach (IPart part in item.Parts) part.ItemId = id;
    }

    /// <summary>
    /// Gets the exported item ID set from the context data.
    /// </summary>
    /// <param name="context">The entry set context containing the data
    /// collection.</param>
    /// <param name="name">The optional name of the set to get, i.e. the name
    /// of the file it was loaded from without its extension. When null,
    /// the merged set of all the loaded sets is returned.</param>
    /// <returns>The exported item ID set stored in the context data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the context data
    /// does not contain an exported item ID set at the expected key.</exception>
    public static ExportedItemIdSet GetItemIdsSet(CadmusEntrySetContext context,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        string key = GetItemIdsKey(name);
        return context.Data.TryGetValue(key, out object? value) &&
            value is ExportedItemIdSet set
            ? set
            : throw new InvalidOperationException(
                $"No item IDs set \"{name ?? "*"}\" found in the data context");
    }

    /// <summary>
    /// Tries to get the exported item ID set from the context data.
    /// </summary>
    /// <param name="context">The entry set context containing the data
    /// collection.</param>
    /// <param name="name">The optional name of the set to get.</param>
    /// <returns>The set or null if not found.</returns>
    public static ExportedItemIdSet? TryGetItemIdsSet(
        CadmusEntrySetContext context, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        return context.Data.TryGetValue(GetItemIdsKey(name), out object? value)
            ? value as ExportedItemIdSet
            : null;
    }

    /// <summary>
    /// Finds the item with the specified EID in the specified set. If more
    /// than a single item has the same EID, a warning is logged and the first
    /// one is returned.
    /// </summary>
    /// <param name="idSet">The set of exported item IDs.</param>
    /// <param name="eid">The EID to find.</param>
    /// <param name="logger">The optional logger.</param>
    /// <returns>The item IDs or null if not found.</returns>
    public static ExportedItemId? FindItemId(ExportedItemIdSet idSet, string eid,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(idSet);
        ArgumentNullException.ThrowIfNull(eid);

        IReadOnlyList<ExportedItemId> ids = idSet.GetAll(eid);
        if (ids.Count > 1)
        {
            logger?.LogWarning("EID {Eid} is ambiguous: found in {Count} items " +
                "({Items}); using the first one", eid, ids.Count,
                string.Join(", ", ids.Select(i => i.Id)));
        }
        return ids.Count > 0 ? ids[0] : null;
    }

    /// <summary>
    /// Add an internal link to the specified <see cref="PinLinksPart"/>.
    /// </summary>
    /// <param name="part">The <see cref="PinLinksPart"/> to add the link to.
    /// </param>
    /// <param name="eid">The EID of the target item.</param>
    /// <param name="idSet">The set of exported item IDs.</param>
    /// <param name="logger">The optional logger.</param>
    /// <param name="scope">The optional scope of the link.</param>
    /// <returns>The added link, or null if the target was not found.
    /// </returns>
    public static AssertedCompositeId? AddInternalLink(PinLinksPart part,
        string eid, ExportedItemIdSet idSet, ILogger? logger = null,
        string? scope = null)
    {
        ArgumentNullException.ThrowIfNull(part);
        ArgumentNullException.ThrowIfNull(eid);
        ArgumentNullException.ThrowIfNull(idSet);

        // find target item from EID (error if not found)
        ExportedItemId? id = FindItemId(idSet, eid, logger);
        if (id == null)
        {
            logger?.LogError("Target ID {Eid} not found in item IDs set",
                eid);
            return null;
        }
        if (id.MetadataPartId == null)
        {
            logger?.LogError("Target ID {Eid} has no metadata part in item " +
                "IDs set", eid);
            return null;
        }

        AssertedCompositeId link = new()
        {
            Scope = scope,
            Target = new PinTarget
            {
                Gid = "P" + id.MetadataPartId,
                Label = $"{eid}|{id.Id} (metadata)",
                ItemId = id.Id,
                PartId = id.MetadataPartId,
                PartTypeId = "it.vedph.metadata",
                Name = "eid",
                Value = eid
            }
        };
        part.Links.Add(link);

        return link;
    }
}
