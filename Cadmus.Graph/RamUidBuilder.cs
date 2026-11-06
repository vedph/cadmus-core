using System;
using System.Collections.Generic;

namespace Cadmus.Graph;

/// <summary>
/// RAM-based UID builder. This is essentially used for diagnostic or
/// demo purposes.
/// </summary>
public sealed class RamUidBuilder : IUidBuilder
{
    private readonly object _lock = new();
    private int _nextId;

    // key=unsuffixed UID (lowercase), value=entries with that UID
    private readonly Dictionary<string, List<UidEntry>> _lookup = [];

    /// <summary>
    /// Build the eventually suffixed UID, with the same logic of the
    /// database-based builders (see <see cref="UidResolver"/>).
    /// </summary>
    /// <param name="unsuffixed">The UID as calculated from its source,
    /// without any suffix. If the caller wants a unique new UID for it, it
    /// must be suffixed with <c>##</c>: this suffix will be either removed
    /// when no entry is present with the same <paramref name="unsuffixed"/>
    /// value, or replaced by a numeric suffix preceded by <c>#</c> to grant
    /// its uniqueness.</param>
    /// <param name="sid">The source ID (SID).</param>
    /// <returns>UID, eventually suffixed with #N.</returns>
    /// <exception cref="ArgumentException">sid or unsuffixed</exception>
    public string BuildUid(string unsuffixed, string sid)
    {
        if (string.IsNullOrEmpty(sid))
            throw new ArgumentException(null, nameof(sid));
        if (string.IsNullOrEmpty(unsuffixed))
            throw new ArgumentException(null, nameof(unsuffixed));

        (string uid, bool unique) = UidResolver.Parse(unsuffixed);
        string key = uid.ToLowerInvariant();

        lock (_lock)
        {
            if (!_lookup.TryGetValue(key, out List<UidEntry>? entries))
                _lookup[key] = entries = [];

            string? resolved = UidResolver.TryResolve(entries, uid, sid,
                unique, out bool hasSuffix);
            if (resolved != null) return resolved;

            UidEntry entry = new()
            {
                Id = ++_nextId,
                Sid = sid,
                Unsuffixed = uid,
                HasSuffix = hasSuffix
            };
            entries.Add(entry);
            return UidResolver.BuildUid(uid, entry.Id, hasSuffix);
        }
    }
}
