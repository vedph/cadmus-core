using Cadmus.Core.Config;
using CsvHelper.Configuration;
using CsvHelper;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace Cadmus.Import;

/// <summary>
/// CSV thesaurus reader. This reads thesaurus entries from a CSV file,
/// having a column for thesaurus ID (named <c>thesaurusId</c>, case insensitive)
/// and either one for entry ID and one for entry value (named <c>name</c> and
/// <c>value</c>, case insensitive), or just one for target ID (for aliases,
/// named <c>targetId</c>, case insensitive).
/// </summary>
/// <seealso cref="IThesaurusReader" />
public sealed class CsvThesaurusReader : IThesaurusReader
{
    private readonly CsvReader _reader;
    private bool _disposed;
    private CsvThesaurusEntry? _pendingEntry;

    /// <summary>
    /// Gets the current thesaurus if any.
    /// </summary>
    public Thesaurus? Current { get; private set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="CsvThesaurusReader"/> class.
    /// </summary>
    /// <param name="stream">The input stream.</param>
    /// <exception cref="ArgumentNullException">stream</exception>
    public CsvThesaurusReader(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        CsvConfiguration config = new(CultureInfo.InvariantCulture)
        {
            PrepareHeaderForMatch = args => args.Header.ToLower(),
            TrimOptions = TrimOptions.Trim
        };
        _reader = new CsvReader(new StreamReader(stream, Encoding.UTF8), config);
        if (_reader.Read()) _reader.ReadHeader();
    }

    private static ThesaurusEntry GetThesaurusEntry(CsvThesaurusEntry entry)
        => new()
        {
            Id = entry.Id!,
            Value = entry.Value ?? "",
        };

    /// <summary>
    /// Read the next thesaurus entry from source.
    /// </summary>
    /// <returns>Thesaurus, or null if no more thesauri in source.</returns>
    public bool Next()
    {
        // read the next entry if any
        CsvThesaurusEntry? entry;
        if (_pendingEntry != null)
        {
            entry = _pendingEntry;
            _pendingEntry = null;
        }
        else if (!_reader.Read())
        {
            Current = null;
            return false;
        }
        else
        {
            entry = _reader.GetRecord<CsvThesaurusEntry>();
            if (entry == null)
            {
                Current = null;
                return false;
            }
        }

        // read all the entries until id changes, but just return an alias
        // thesaurus when the entry just read has a target id
        Thesaurus thesaurus = new()
        {
            Id = entry.ThesaurusId!
        };
        if (!string.IsNullOrEmpty(entry.TargetId))
        {
            thesaurus.TargetId = entry.TargetId;
            Current = thesaurus;
            return true;
        }
        if (entry.Id != null) thesaurus.AddEntry(GetThesaurusEntry(entry));
        while (_reader.Read())
        {
            entry = _reader.GetRecord<CsvThesaurusEntry>();
            // supply an implicit (empty) thesaurus ID
            if (entry?.ThesaurusId?.Length == 0) entry.ThesaurusId = thesaurus.Id;
            if (entry?.ThesaurusId != thesaurus.Id)
            {
                _pendingEntry = entry;
                break;
            }
            if (entry?.Id != null) thesaurus.AddEntry(GetThesaurusEntry(entry));
        }
        if (thesaurus.Entries.Count == 0)
        {
            Current = null;
            return false;
        }
        Current = thesaurus;
        return true;
    }

    private void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _reader.Dispose();
            }
            _disposed = true;
        }
    }

    /// <summary>
    /// Performs application-defined tasks associated with freeing, releasing,
    /// or resetting unmanaged resources.
    /// </summary>
    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Serializable CSV thesaurus entry. This is used to read the CSV file,
/// and then convert to a <see cref="ThesaurusEntry"/> when needed.
/// </summary>
internal class CsvThesaurusEntry
{
    public string? ThesaurusId { get; set; }
    public string? Id { get; set; }
    public string? Value { get; set; }
    public string? TargetId { get; set; }

    public override string ToString()
    {
        return $"{ThesaurusId}: " + (string.IsNullOrEmpty(TargetId)
            ? $"{Id}={Value}" : TargetId);
    }
}