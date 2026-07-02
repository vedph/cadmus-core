using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Turtle format RDF writer.
/// </summary>
public sealed class TurtleRdfWriter : RdfWriter
{
    /// <summary>
    /// Creates a new RDF writer.
    /// </summary>
    /// <param name="settings">The RDF export settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    public TurtleRdfWriter(RdfExportSettings? settings = null,
        Dictionary<string, string>? prefixMappings = null,
        Dictionary<int, string>? uriMappings = null)
        : base(settings, prefixMappings, uriMappings)
    {
    }

    /// <summary>
    /// Writes the header, including prefixes and base URI if any.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    public override async Task WriteHeaderAsync(TextWriter writer)
    {
        if (_settings.IncludePrefixes)
        {
            foreach (KeyValuePair<string, string> mapping in
                _prefixMappings)
            {
                await writer.WriteLineAsync(
                    $"@prefix {mapping.Key}: <{mapping.Value}> .");
            }
            await writer.WriteLineAsync();
        }

        if (!string.IsNullOrEmpty(_settings.BaseUri))
        {
            await writer.WriteLineAsync($"@base <{_settings.BaseUri}> .");
            await writer.WriteLineAsync();
        }

        if (_settings.IncludeComments)
        {
            await writer.WriteLineAsync(
                "# RDF data exported from Cadmus Graph database");
            await writer.WriteLineAsync(
                $"# Export date: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
            await writer.WriteLineAsync();
        }
    }

    /// <summary>
    /// Writes the given triples.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">writer or triples</exception>"
    public override async Task WriteAsync(TextWriter writer,
        List<RdfTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(triples);

        foreach (RdfTriple triple in triples)
        {
            await WriteTripleAsync(writer, triple);
        }
    }

    private async Task WriteTripleAsync(TextWriter writer, RdfTriple triple)
    {
        string subject = GetUriForId(triple.SubjectId);
        string predicate = GetUriForId(triple.PredicateId);

        // format subject - use angle brackets for full URIs, no brackets
        // for prefixed URIs
        string formattedSubject = subject.Contains(':') &&
            !subject.StartsWith("http") ? subject : $"<{GetFullUri(triple.SubjectId)}>";

        // format predicate
        string formattedPredicate = predicate.Contains(':') && !predicate.StartsWith("http")
            ? predicate
            : $"<{GetFullUri(triple.PredicateId)}>";

        // format object
        string formattedObject;
        if (!string.IsNullOrEmpty(triple.ObjectLiteral))
        {
            formattedObject = EscapeLiteral(triple.ObjectLiteral,
                triple.ObjectLiteralType, triple.ObjectLiteralLanguage);
        }
        else if (triple.ObjectId.HasValue)
        {
            string objectUri = GetUriForId(triple.ObjectId.Value);
            formattedObject = objectUri.Contains(':') && !objectUri.StartsWith("http")
                ? objectUri
                : $"<{GetFullUri(triple.ObjectId.Value)}>";
        }
        else
        {
            throw new InvalidOperationException($"Triple {triple.Id} " +
                $"has neither object URI nor literal value");
        }

        if (_settings.PrettyPrint)
        {
            await writer.WriteLineAsync(
                $"{formattedSubject} {formattedPredicate} {formattedObject} .");
        }
        else
        {
            await writer.WriteLineAsync(
                $"{formattedSubject} {formattedPredicate} {formattedObject} .");
        }
    }

    /// <summary>
    /// Writes the footer, if any.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>"
    public override async Task WriteFooterAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (_settings.IncludeComments)
        {
            await writer.WriteLineAsync();
            await writer.WriteLineAsync("# End of RDF data");
        }
    }
}
