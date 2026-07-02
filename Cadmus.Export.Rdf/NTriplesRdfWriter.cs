using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// N-Triples format RDF writer.
/// </summary>
public sealed class NTriplesRdfWriter : RdfWriter
{
    /// <summary>
    /// Creates a new N-Triples writer.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    public NTriplesRdfWriter(RdfExportSettings? settings = null,
        Dictionary<string, string>? prefixMappings = null,
        Dictionary<int, string>? uriMappings = null)
        : base(settings, prefixMappings, uriMappings)
    {
    }

    /// <summary>
    /// Writes the header, including comments if any.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>"
    public override async Task WriteHeaderAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (_settings.IncludeComments)
        {
            await writer.WriteLineAsync(
                "# RDF data exported from Cadmus Graph database");
            await writer.WriteLineAsync(
                $"# Export date: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}");
        }
    }

    /// <summary>
    /// Writes the given triples in N-Triples format.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="triples">The triples to write.</param>
    /// <exception cref="InvalidOperationException">writer or triples</exception>
    public override async Task WriteAsync(TextWriter writer, List<RdfTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(triples);

        foreach (RdfTriple triple in triples)
        {
            string subject = $"<{GetFullUri(triple.SubjectId)}>";
            string predicate = $"<{GetFullUri(triple.PredicateId)}>";

            string objectValue;
            if (!string.IsNullOrEmpty(triple.ObjectLiteral))
            {
                objectValue = EscapeLiteral(triple.ObjectLiteral,
                    triple.ObjectLiteralType, triple.ObjectLiteralLanguage);
            }
            else if (triple.ObjectId.HasValue)
            {
                objectValue = $"<{GetFullUri(triple.ObjectId.Value)}>";
            }
            else
            {
                throw new InvalidOperationException($"Triple {triple.Id} " +
                    $"has neither object URI nor literal value");
            }

            await writer.WriteLineAsync($"{subject} {predicate} {objectValue} .");
        }
    }

    /// <summary>
    /// Writes the footer, including comments if any.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>""
    public override async Task WriteFooterAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (_settings.IncludeComments)
            await writer.WriteLineAsync("# End of RDF data");
    }
}
