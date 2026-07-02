using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Base class for RDF format writers.
/// </summary>
public abstract class RdfWriter
{
    /// <summary>
    /// The RDF export settings.
    /// </summary>
    protected readonly RdfExportSettings _settings;

    /// <summary>
    /// The prefix mappings.
    /// </summary>
    protected readonly Dictionary<string, string> _prefixMappings;

    /// <summary>
    /// The URI mappings.
    /// </summary>
    protected readonly Dictionary<int, string> _uriMappings;

    /// <summary>
    /// Creates a new RDF writer.
    /// </summary>
    /// <param name="settings">The optional RDF export settings.</param>
    /// <param name="prefixMappings">The optional preset prefix mappings.</param>
    /// <param name="uriMappings">The optional preset URI mappings.</param>
    /// <exception cref="ArgumentNullException">settings or mappings</exception>
    protected RdfWriter(RdfExportSettings? settings = null,
        Dictionary<string, string>? prefixMappings = null,
        Dictionary<int, string>? uriMappings = null)
    {
        _settings = settings ?? new RdfExportSettings();
        _prefixMappings = prefixMappings ?? [];
        _uriMappings = uriMappings ?? [];
    }

    /// <summary>
    /// Get the URI mapped to the given numeric ID.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The URI.</returns>
    /// <exception cref="InvalidOperationException">ID not mapped.</exception>
    protected string GetUriForId(int id)
    {
        if (_uriMappings.TryGetValue(id, out string? uri))
        {
            if (uri == null)
            {
                throw new InvalidOperationException(
                    $"URI not found for ID {id}: {uri}");
            }
            return uri;
        }
        throw new InvalidOperationException($"No URI mapping found for ID: {id}");
    }

    /// <summary>
    /// Get the full URI (expanded) for the given numeric ID.
    /// </summary>
    /// <param name="id">The ID.</param>
    /// <returns>The URI.</returns>
    protected string GetFullUri(int id)
    {
        string shortUri = GetUriForId(id);
        return UriHelper.ExpandUri(shortUri, _prefixMappings);
    }

    /// <summary>
    /// Escapes a literal string for RDF output.
    /// </summary>
    /// <param name="literal">The literal to escape.</param>
    /// <param name="literalType">The optional literal type (datatype).</param>
    /// <param name="literalLanguage">The optional literal language.</param>
    /// <returns>Escaped literal.</returns>
    protected static string EscapeLiteral(string literal,
        string? literalType = null, string? literalLanguage = null)
    {
        if (string.IsNullOrEmpty(literal)) return "\"\"";

        string escapedValue = "\"" + literal
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t") + "\"";

        // add language tag if present (e.g. "Hello"@en):
        // language tags take precedence over datatypes as per RDF spec
        if (!string.IsNullOrEmpty(literalLanguage))
            return escapedValue + "@" + literalLanguage;

        // add datatype if present (e.g., "12.3"^^xsd:double)
        if (!string.IsNullOrEmpty(literalType))
        {
            // check if type is a full URI or prefixed
            string typeUri = literalType.Contains(':') &&
                !literalType.StartsWith("http")
                ? literalType
                : $"<{literalType}>";
            return escapedValue + "^^" + typeUri;
        }

        return escapedValue;
    }

    /// <summary>
    /// Write the given triples to the given writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="triples">The triples to write.</param>
    public abstract Task WriteAsync(TextWriter writer, List<RdfTriple> triples);

    /// <summary>
    /// Write the header to the given writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    public abstract Task WriteHeaderAsync(TextWriter writer);

    /// <summary>
    /// Write the footer to the given writer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    public abstract Task WriteFooterAsync(TextWriter writer);
}
