using System.Collections.Generic;
using System.Text;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Configuration settings for RDF export.
/// </summary>
public class RdfExportSettings
{
    /// <summary>
    /// The RDF format to export (turtle, rdfxml, ntriples, jsonld).
    /// Default is "turtle".
    /// </summary>
    public string Format { get; set; } = "turtle";

    /// <summary>
    /// Whether to include prefix declarations in the output.
    /// Default is true.
    /// </summary>
    public bool IncludePrefixes { get; set; } = true;

    /// <summary>
    /// Whether to include comments in the output.
    /// Default is true.
    /// </summary>
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// The base URI to use for relative URIs.
    /// If null or empty, no base URI is used.
    /// </summary>
    public string? BaseUri { get; set; }

    /// <summary>
    /// Maximum number of triples to process in a single batch.
    /// Default is 10000.
    /// </summary>
    public int BatchSize { get; set; } = 10000;

    /// <summary>
    /// Whether to pretty-print the output (add indentation and line breaks).
    /// Default is true.
    /// </summary>
    public bool PrettyPrint { get; set; } = true;

    /// <summary>
    /// Whether to export only nodes that are referenced in triples.
    /// Default is false (exports all nodes).
    /// </summary>
    public bool ExportReferencedNodesOnly { get; set; } = false;

    /// <summary>
    /// Optional filter for node tags. If specified, only nodes with matching
    /// tags are exported.
    /// </summary>
    public HashSet<string>? NodeTagFilter { get; set; }

    /// <summary>
    /// Optional filter for triple tags. If specified, only triples with
    /// matching tags are exported.
    /// </summary>
    public HashSet<string>? TripleTagFilter { get; set; }

    /// <summary>
    /// The character encoding to use for output files.
    /// Default is UTF-8.
    /// </summary>
    public Encoding Encoding { get; set; } = Encoding.UTF8;
}
