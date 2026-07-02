using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// JSON-LD format RDF writer.
/// </summary>
public sealed class JsonLdRdfWriter : RdfWriter
{
    /// <summary>
    /// Creates a new JSON-LD writer.
    /// </summary>
    /// <param name="settings">The RDF export settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    public JsonLdRdfWriter(RdfExportSettings settings,
        Dictionary<string, string> prefixMappings,
        Dictionary<int, string> uriMappings)
        : base(settings, prefixMappings, uriMappings)
    {
    }

    private static string JsonEscape(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;

        return text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t")
            .Replace("\b", "\\b")
            .Replace("\f", "\\f");
    }

    /// <summary>
    /// Writes the header.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public override async Task WriteHeaderAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        await writer.WriteLineAsync("{");

        if (_settings.IncludeComments)
        {
            await writer.WriteLineAsync($"  \"@comment\": " +
                $"\"RDF data exported from Cadmus Graph database at " +
                $"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",");
        }

        // write @context
        await writer.WriteLineAsync("  \"@context\": {");

        List<string> contextEntries = [];
        foreach (KeyValuePair<string, string> mapping in _prefixMappings)
        {
            contextEntries.Add(
                $"    \"{JsonEscape(mapping.Key)}\": \"{JsonEscape(mapping.Value)}\"");
        }

        if (!string.IsNullOrEmpty(_settings.BaseUri))
        {
            contextEntries.Add(
                $"    \"@base\": \"{JsonEscape(_settings.BaseUri)}\"");
        }

        await writer.WriteLineAsync(string.Join(",\n", contextEntries));
        await writer.WriteLineAsync("  },");
        await writer.WriteLineAsync("  \"@graph\": [");
    }

    /// <summary>
    /// Writes the given triples.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">writer or triples</exception>
    public override async Task WriteAsync(TextWriter writer,
        List<RdfTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(triples);

        // group triples by subject
        var groupedTriples = triples.GroupBy(t => t.SubjectId).ToList();

        for (int i = 0; i < groupedTriples.Count; i++)
        {
            var subjectGroup = groupedTriples[i];
            string subjectUri = GetUriForId(subjectGroup.Key);

            await writer.WriteLineAsync("    {");
            await writer.WriteLineAsync(
                $"      \"@id\": \"{JsonEscape(subjectUri)}\",");

            // group properties by predicate
            var predicateGroups = subjectGroup.GroupBy(t => t.PredicateId).ToList();

            for (int j = 0; j < predicateGroups.Count; j++)
            {
                var predicateGroup = predicateGroups[j];
                string predicateUri = GetUriForId(predicateGroup.Key);

                List<string> values = [];
                foreach (RdfTriple triple in predicateGroup)
                {
                    if (!string.IsNullOrEmpty(triple.ObjectLiteral))
                    {
                        // literal value
                        StringBuilder literalValue = new("{");
                        literalValue.Append($"\"@value\": " +
                            $"\"{JsonEscape(triple.ObjectLiteral)}\"");

                        if (!string.IsNullOrEmpty(triple.ObjectLiteralLanguage))
                        {
                            literalValue.Append(
                                $", \"@language\": \"{JsonEscape(triple.ObjectLiteralLanguage)}\"");
                        }
                        else if (!string.IsNullOrEmpty(triple.ObjectLiteralType))
                        {
                            string datatype =
                                triple.ObjectLiteralType.Contains(':') &&
                                !triple.ObjectLiteralType.StartsWith("http")
                                ? triple.ObjectLiteralType
                                : triple.ObjectLiteralType;
                            literalValue.Append($", \"@type\": \"{JsonEscape(datatype)}\"");
                        }

                        literalValue.Append('}');
                        values.Add(literalValue.ToString());
                    }
                    else if (triple.ObjectId.HasValue)
                    {
                        // resource reference
                        string objectUri = GetUriForId(triple.ObjectId.Value);
                        values.Add($"{{\"@id\": \"{JsonEscape(objectUri)}\"}}");
                    }
                }

                string propertyOutput;
                if (values.Count == 1)
                {
                    propertyOutput =
                        $"      \"{JsonEscape(predicateUri)}\": {values[0]}";
                }
                else
                {
                    string valuesJson = string.Join(", ", values);
                    propertyOutput =
                        $"      \"{JsonEscape(predicateUri)}\": [{valuesJson}]";
                }

                if (j < predicateGroups.Count - 1) propertyOutput += ",";

                await writer.WriteLineAsync(propertyOutput);
            }

            string closingBrace = i < groupedTriples.Count - 1 ? "    }," : "    }";
            await writer.WriteLineAsync(closingBrace);
        }
    }

    /// <summary>
    /// Writes the footer.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public override async Task WriteFooterAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        await writer.WriteLineAsync("  ]");
        await writer.WriteLineAsync("}");
    }
}
