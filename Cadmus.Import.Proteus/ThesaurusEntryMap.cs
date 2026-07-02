using Cadmus.Core.Config;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Cadmus.Import.Proteus;

/// <summary>
/// A map of thesauri entries to be used when importing data so that any
/// entry value can be encoded with its corresponding ID.
/// </summary>
public class ThesaurusEntryMap
{
    private Dictionary<string, Thesaurus> _thesauri;
    private Dictionary<string, string> _aliases;

    /// <summary>
    /// Gets or sets the default alias language. This is the language to append
    /// to alias IDs when loading thesauri.
    /// </summary>
    public string DefaultAliasLanguage { get; set; }

    /// <summary>
    /// Gets the count of thesauri loaded in the map.
    /// </summary>
    public int Count => _thesauri.Count;

    /// <summary>
    /// Initializes a new instance of the <see cref="ThesaurusEntryMap"/> class.
    /// </summary>
    public ThesaurusEntryMap()
    {
        _thesauri = [];
        _aliases = [];
        DefaultAliasLanguage = "en";
    }

    /// <summary>
    /// Loads an array of thesauri from the specified stream using a
    /// <see cref="JsonThesaurusReader"/>.
    /// </summary>
    /// <param name="stream">The stream.</param>
    public void Load(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonThesaurusReader reader = new(stream);
        List<Thesaurus> thesauri = [];
        while ((reader.Next()))
        {
            thesauri.Add(reader.Current!);
        }

        _thesauri = thesauri.Where(t => t.TargetId == null)
            .ToDictionary(t => t.Id);
        _aliases = thesauri.Where(t => t.TargetId != null)
            .ToDictionary(t => t.Id, t => t.TargetId! + "@" + DefaultAliasLanguage);
    }

    /// <summary>
    /// Gets the thesaurus with the specified ID.
    /// </summary>
    /// <param name="id">The identifier. If this is an alias, it will be
    /// automatically remapped into the target ID.</param>
    /// <returns>Thesaurus, or null if not found.</returns>
    /// <exception cref="ArgumentNullException">id</exception>
    public Thesaurus? GetThesaurus(string id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (_aliases.TryGetValue(id, out string? alias))
            id = alias;

        return _thesauri.TryGetValue(id, out Thesaurus? thesaurus)
            ? thesaurus
            : null;
    }

    /// <summary>
    /// Gets the entry identifier corresponding to the specified entry value
    /// in the specified thesaurus.
    /// </summary>
    /// <param name="thesaurusId">The thesaurus identifier. If this is an alias,
    /// it will be automatically remapped into the target ID.</param>
    /// <param name="entryValue">The entry value.</param>
    /// <returns>Entry ID, or null if not found.</returns>
    public string? GetEntryId(string thesaurusId, string entryValue)
    {
        ArgumentNullException.ThrowIfNull(thesaurusId);
        ArgumentNullException.ThrowIfNull(entryValue);

        if (_aliases.TryGetValue(thesaurusId, out string? alias))
            thesaurusId = alias;

        if (!_thesauri.TryGetValue(thesaurusId, out Thesaurus? thesaurus))
            return null;

        return thesaurus.Entries.FirstOrDefault(e => e.Value == entryValue)?.Id;
    }
}
