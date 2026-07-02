using Cadmus.Core.Config;
using System.Linq;

namespace Cadmus.Import;

/// <summary>
/// Thesaurus import helper methods.
/// </summary>
public static class ThesaurusHelper
{
    /// <summary>
    /// Copies entries from source to target thesaurus.
    /// </summary>
    /// <param name="source">The source (imported thesaurus). If this is an
    /// alias, the output will be an alias too, without any entries.</param>
    /// <param name="target">The target, or null when no existing thesaurus
    /// is found; this will just return a new thesaurus with all the entries
    /// from <paramref name="source"/>.</param>
    /// <param name="mode">The mode.</param>
    /// <returns>A new thesaurus resulting from copying source vs. target
    /// entries.</returns>
    public static Thesaurus CopyThesaurus(Thesaurus source, Thesaurus? target,
        ImportUpdateMode mode)
    {
        Thesaurus result = new()
        {
            Id = source.Id
        };

        // replace mode or source is alias: just copy
        if (mode == ImportUpdateMode.Replace ||
            !string.IsNullOrEmpty(source.TargetId))
        {
            foreach (ThesaurusEntry se in source.Entries) result.AddEntry(se);
            result.TargetId = source.TargetId;
            return result;
        }

        // patch/synch mode:
        // - add source entries missing in target,
        // - update source entries existing in target.
        if (target != null)
        {
            foreach (ThesaurusEntry te in target.Entries) result.AddEntry(te);
        }

        foreach (ThesaurusEntry se in source.Entries)
        {
            ThesaurusEntry? te = result.Entries.FirstOrDefault(
                e => e.Id == se.Id);
            if (te == null) result.Entries.Add(se);
            else te.Value = se.Value;
        }

        // synch mode:
        // - remove result entries missing in source.
        if (mode == ImportUpdateMode.Synch)
        {
            for (int i = result.Entries.Count - 1; i >= 0; i--)
            {
                  if (source.Entries.All(e => e.Id != result.Entries[i].Id))
                    result.Entries.RemoveAt(i);
            }
        }
        return result;
    }
}

/// <summary>
/// Import update mode.
/// </summary>
public enum ImportUpdateMode
{
    /// <summary>
    /// Replace mode: the imported entry fully replaces the existing one.
    /// </summary>
    Replace = 0,
    /// <summary>
    /// Patch mode: the imported entry is merged with the existing one, i.e.
    /// all the existing values are kept or updated, and the new ones are added.
    /// </summary>
    Patch,
    /// <summary>
    /// Synch mode: the imported entry is synched with the existing one, i.e.
    /// all the existing values are kept or updated, all the new ones are added,
    /// all the existing values missing from the new entry are removed.
    /// </summary>
    Synch
}
