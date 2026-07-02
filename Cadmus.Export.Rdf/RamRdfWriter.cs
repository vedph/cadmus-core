using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// In-memory RDF writer for testing purposes.
/// Stores all written data in memory for easy verification.
/// </summary>
public sealed class RamRdfWriter : RdfWriter
{
    private readonly List<string> _headerLines = [];
    private readonly List<string> _footerLines = [];
    private readonly List<RdfTriple> _writtenTriples = [];
    private readonly List<string> _tripleLines = [];

    /// <summary>
    /// Create an in-memory RDF writer for testing purposes.
    /// </summary>
    /// <param name="settings">The RDF export settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    public RamRdfWriter(RdfExportSettings? settings = null,
        Dictionary<string, string>? prefixMappings = null,
        Dictionary<int, string>? uriMappings = null)
        : base(settings, prefixMappings, uriMappings)
    {
    }

    /// <summary>
    /// Gets all header lines written to this writer.
    /// </summary>
    public IReadOnlyList<string> HeaderLines => _headerLines.AsReadOnly();

    /// <summary>
    /// Gets all footer lines written to this writer.
    /// </summary>
    public IReadOnlyList<string> FooterLines => _footerLines.AsReadOnly();

    /// <summary>
    /// Gets all triples that were written to this writer.
    /// </summary>
    public IReadOnlyList<RdfTriple> WrittenTriples => _writtenTriples.AsReadOnly();

    /// <summary>
    /// Gets all triple lines (formatted output) written to this writer.
    /// </summary>
    public IReadOnlyList<string> TripleLines => _tripleLines.AsReadOnly();

    /// <summary>
    /// Gets the complete output as it would appear in a file.
    /// </summary>
    public string CompleteOutput
    {
        get
        {
            StringBuilder sb = new();
            foreach (string line in _headerLines) sb.AppendLine(line);
            foreach (string line in _tripleLines) sb.AppendLine(line);
            foreach (string line in _footerLines) sb.AppendLine(line);
            return sb.ToString();
        }
    }

    /// <summary>
    /// Gets statistics about the written data.
    /// </summary>
    public RdfWriterStatistics Statistics => new()
    {
        TripleCount = _writtenTriples.Count,
        LiteralTripleCount = _writtenTriples.Count(
            t => !string.IsNullOrEmpty(t.ObjectLiteral)),
        ResourceTripleCount = _writtenTriples.Count(t => t.ObjectId.HasValue),
        UniqueSubjects = _writtenTriples.Select(
            t => t.SubjectId).Distinct().Count(),
        UniquePredicates = _writtenTriples.Select(
            t => t.PredicateId).Distinct().Count(),
        UniqueObjects = _writtenTriples.Where(t => t.ObjectId.HasValue)
            .Select(t => t.ObjectId!.Value).Distinct().Count(),
        HeaderLineCount = _headerLines.Count,
        FooterLineCount = _footerLines.Count,
        OutputLineCount = _headerLines.Count
            + _tripleLines.Count + _footerLines.Count
    };

    /// <summary>
    /// Writes the RDF header to the given writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public override async Task WriteHeaderAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        StringWriter stringWriter = new();

        if (_settings.IncludePrefixes)
        {
            foreach (KeyValuePair<string, string> mapping in _prefixMappings)
            {
                string line = $"@prefix {mapping.Key}: <{mapping.Value}> .";
                await stringWriter.WriteLineAsync(line);
                _headerLines.Add(line);
            }
            await stringWriter.WriteLineAsync();
            _headerLines.Add(string.Empty);
        }

        if (!string.IsNullOrEmpty(_settings.BaseUri))
        {
            string line = $"@base <{_settings.BaseUri}> .";
            await stringWriter.WriteLineAsync(line);
            _headerLines.Add(line);
            await stringWriter.WriteLineAsync();
            _headerLines.Add(string.Empty);
        }

        if (_settings.IncludeComments)
        {
            string line1 = "# RDF data exported from Cadmus Graph database";
            string line2 = $"# Export date: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}";
            await stringWriter.WriteLineAsync(line1);
            await stringWriter.WriteLineAsync(line2);
            await stringWriter.WriteLineAsync();
            _headerLines.Add(line1);
            _headerLines.Add(line2);
            _headerLines.Add(string.Empty);
        }

        // write to the actual writer
        await writer.WriteAsync(stringWriter.ToString());
    }

    /// <summary>
    /// Writes the given triples to the given writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="triples">The triples to write.</param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">writer or triples</exception>
    public override async Task WriteAsync(TextWriter writer,
        List<RdfTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(triples);

        foreach (RdfTriple triple in triples)
        {
            // store the triple for testing
            _writtenTriples.Add(triple);

            // format the triple
            string subject = GetUriForId(triple.SubjectId);
            string predicate = GetUriForId(triple.PredicateId);

            string formattedSubject = subject.Contains(':') &&
                !subject.StartsWith("http")
                ? subject
                : $"<{GetFullUri(triple.SubjectId)}>";

            string formattedPredicate = predicate.Contains(':') &&
                !predicate.StartsWith("http")
                ? predicate
                : $"<{GetFullUri(triple.PredicateId)}>";

            string formattedObject;
            if (!string.IsNullOrEmpty(triple.ObjectLiteral))
            {
                formattedObject = EscapeLiteral(triple.ObjectLiteral,
                    triple.ObjectLiteralType, triple.ObjectLiteralLanguage);
            }
            else if (triple.ObjectId.HasValue)
            {
                string objectUri = GetUriForId(triple.ObjectId.Value);
                formattedObject = objectUri.Contains(':') &&
                    !objectUri.StartsWith("http")
                    ? objectUri
                    : $"<{GetFullUri(triple.ObjectId.Value)}>";
            }
            else
            {
                throw new InvalidOperationException(
                    $"Triple {triple.Id} has neither object URI nor literal value");
            }

            string tripleLine = $"{formattedSubject} {formattedPredicate} " +
                $"{formattedObject} .";
            _tripleLines.Add(tripleLine);
            await writer.WriteLineAsync(tripleLine);
        }
    }

    /// <summary>
    /// Writes the RDF footer to the given writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public override async Task WriteFooterAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (_settings.IncludeComments)
        {
            await writer.WriteLineAsync();
            string line = "# End of RDF data";
            await writer.WriteLineAsync(line);
            _footerLines.Add(string.Empty);
            _footerLines.Add(line);
        }
    }

    /// <summary>
    /// Clears all stored data. Useful for reusing the writer in multiple tests.
    /// </summary>
    public void Clear()
    {
        _headerLines.Clear();
        _footerLines.Clear();
        _writtenTriples.Clear();
        _tripleLines.Clear();
    }

    /// <summary>
    /// Finds triples by subject URI (supports both full and prefixed URIs).
    /// </summary>
    public List<RdfTriple> FindTriplesBySubject(string subjectUri)
    {
        return _writtenTriples.Where(t =>
        {
            string uri = GetUriForId(t.SubjectId);
            string fullUri = GetFullUri(t.SubjectId);
            return uri == subjectUri || fullUri == subjectUri;
        }).ToList();
    }

    /// <summary>
    /// Finds triples by predicate URI (supports both full and prefixed URIs).
    /// </summary>
    public List<RdfTriple> FindTriplesByPredicate(string predicateUri)
    {
        return [.. _writtenTriples.Where(t =>
        {
            string uri = GetUriForId(t.PredicateId);
            string fullUri = GetFullUri(t.PredicateId);
            return uri == predicateUri || fullUri == predicateUri;
        })];
    }

    /// <summary>
    /// Finds triples by object URI (supports both full and prefixed URIs).
    /// Only searches resource objects, not literals.
    /// </summary>
    public List<RdfTriple> FindTriplesByObjectResource(string objectUri)
    {
        return [.. _writtenTriples.Where(t =>
        {
            if (!t.ObjectId.HasValue)
                return false;

            string uri = GetUriForId(t.ObjectId.Value);
            string fullUri = GetFullUri(t.ObjectId.Value);
            return uri == objectUri || fullUri == objectUri;
        })];
    }
    
    /// <summary>
    /// Finds triples by literal value (exact match).
    /// </summary>
    public List<RdfTriple> FindTriplesByLiteral(string literalValue)
    {
        return [.. _writtenTriples.Where(t => t.ObjectLiteral == literalValue)];
    }

    /// <summary>
    /// Finds triples by tag.
    /// </summary>
    public List<RdfTriple> FindTriplesByTag(string tag)
    {
        return [.. _writtenTriples.Where(t => t.Tag == tag)];
    }

    /// <summary>
    /// Checks if a specific triple exists (by IDs).
    /// </summary>
    public bool ContainsTriple(int subjectId, int predicateId,
        int? objectId = null, string? objectLiteral = null)
    {
        return _writtenTriples.Any(t =>
            t.SubjectId == subjectId &&
            t.PredicateId == predicateId &&
            t.ObjectId == objectId &&
            t.ObjectLiteral == objectLiteral);
    }

    /// <summary>
    /// Validates that all written triples have valid URIs.
    /// </summary>
    public List<string> ValidateTriples()
    {
        List<string> errors = [];

        foreach (RdfTriple triple in _writtenTriples)
        {
            try
            {
                GetUriForId(triple.SubjectId);
                GetUriForId(triple.PredicateId);
                if (triple.ObjectId.HasValue)
                {
                    GetUriForId(triple.ObjectId.Value);
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Triple {triple.Id}: {ex.Message}");
            }
        }

        return errors;
    }
}
