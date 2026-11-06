using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Cadmus.Graph;

/// <summary>
/// The logic shared by <see cref="IUidBuilder"/> implementations based on
/// a lookup set of <see cref="UidEntry"/>'s, as documented for the
/// <c>uid_lookup</c> table.
/// </summary>
public static class UidResolver
{
    /// <summary>
    /// The suffix to append to an unsuffixed UID to request a unique UID.
    /// </summary>
    public const string UNIQUE_SUFFIX = "##";

    /// <summary>
    /// Parses the UID as received by <see cref="IUidBuilder.BuildUid"/>,
    /// removing the unique-request suffix <c>##</c> when present.
    /// </summary>
    /// <param name="uid">The UID.</param>
    /// <returns>Tuple with the unsuffixed UID and a value indicating whether
    /// a unique UID was requested.</returns>
    /// <exception cref="ArgumentNullException">uid</exception>
    public static (string Unsuffixed, bool Unique) Parse(string uid)
    {
        ArgumentNullException.ThrowIfNull(uid);
        return uid.EndsWith(UNIQUE_SUFFIX, StringComparison.Ordinal)
            ? (uid[..^UNIQUE_SUFFIX.Length], true)
            : (uid, false);
    }

    /// <summary>
    /// Builds the full UID from the specified entry.
    /// </summary>
    /// <param name="unsuffixed">The unsuffixed UID.</param>
    /// <param name="entryId">The entry's ID.</param>
    /// <param name="hasSuffix">True if the entry has a suffix.</param>
    /// <returns>UID.</returns>
    public static string BuildUid(string unsuffixed, int entryId,
        bool hasSuffix) => hasSuffix
            ? unsuffixed + "#" + entryId.ToString(CultureInfo.InvariantCulture)
            : unsuffixed;

    /// <summary>
    /// Tries to resolve the UID from the existing entries having the same
    /// unsuffixed UID (compared case-insensitively). The rules are:
    /// <list type="bullet">
    /// <item><description>if there is no entry, a new entry without suffix
    /// is required, and the UID is equal to the unsuffixed UID;
    /// </description></item>
    /// <item><description>else, if an entry exists with the same SID, it is
    /// reused, so that the same source always gets the same UID (with its
    /// suffix if any). If many exist (in databases written by older
    /// versions), the newest one is reused;</description></item>
    /// <item><description>else, if no unique UID was requested, the UID is
    /// the unsuffixed UID (shared among all the sources generating it), as
    /// first stored;
    /// </description></item>
    /// <item><description>else, a new entry with suffix is required.
    /// </description></item>
    /// </list>
    /// </summary>
    /// <param name="entries">The entries with the same unsuffixed UID.
    /// </param>
    /// <param name="unsuffixed">The unsuffixed UID.</param>
    /// <param name="sid">The SID.</param>
    /// <param name="unique">True if a unique UID is requested.</param>
    /// <param name="newEntryHasSuffix">When the method returns null, this
    /// is true if the new entry to add requires a suffix, false otherwise.
    /// </param>
    /// <returns>The UID, or null if a new entry must be added; in this case,
    /// the UID is <paramref name="unsuffixed"/> when
    /// <paramref name="newEntryHasSuffix"/> is false, or
    /// <paramref name="unsuffixed"/> plus <c>#</c> and the new entry's ID
    /// when it is true.</returns>
    /// <exception cref="ArgumentNullException">entries, unsuffixed or sid
    /// </exception>
    public static string? TryResolve(IEnumerable<UidEntry> entries,
        string unsuffixed, string sid, bool unique,
        out bool newEntryHasSuffix)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(unsuffixed);
        ArgumentNullException.ThrowIfNull(sid);

        newEntryHasSuffix = false;
        IList<UidEntry> list = entries as IList<UidEntry> ?? [.. entries];
        if (list.Count == 0) return null;

        // same source: always reuse its entry. Databases written by versions
        // before 17.1.0 may have many entries for the same source, as a new
        // one was added at each save: the newest one is the one used by the
        // current node, so reusing it does not change the node's UID
        UidEntry? same = list
            .Where(e => string.Equals(e.Sid, sid, StringComparison.Ordinal))
            .OrderByDescending(e => e.Id)
            .FirstOrDefault();
        if (same != null)
            return BuildUid(same.Unsuffixed ?? unsuffixed, same.Id,
                same.HasSuffix);

        // another source: share it unless unique (with the stored form,
        // as UIDs are case-insensitive)
        if (!unique)
        {
            return list.Where(e => !e.HasSuffix).OrderBy(e => e.Id)
                .FirstOrDefault()?.Unsuffixed ?? unsuffixed;
        }

        newEntryHasSuffix = true;
        return null;
    }
}
