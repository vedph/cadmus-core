using System;
using System.Collections.Generic;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Factory class for creating RDF writers based on format.
/// </summary>
public static class RdfWriterFactory
{
    /// <summary>
    /// Creates an RDF writer for the given format.
    /// </summary>
    /// <param name="format">The format.</param>
    /// <param name="settings">The export settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    /// <returns>The RDF writer.</returns>
    /// <exception cref="ArgumentNullException">format, settings,
    /// prefixMappings, or uriMappings</exception>
    /// <exception cref="NotSupportedException">Format not supported.</exception>
    public static RdfWriter CreateWriter(string format,
        RdfExportSettings settings,
        Dictionary<string, string> prefixMappings,
        Dictionary<int, string> uriMappings)
    {
        ArgumentNullException.ThrowIfNull(format);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(prefixMappings);
        ArgumentNullException.ThrowIfNull(uriMappings);

        return format?.ToLowerInvariant() switch
        {
            "turtle" or "ttl" =>
                new TurtleRdfWriter(settings, prefixMappings, uriMappings),
            "ntriples" or "nt" =>
                new NTriplesRdfWriter(settings, prefixMappings, uriMappings),
            "rdfxml" or "rdf" or "xml" =>
                new XmlRdfWriter(settings, prefixMappings, uriMappings),
            "rdfowlxml" or "owlxml" or "owl" =>
                new XmlRdfOwlWriter(settings, prefixMappings, uriMappings),
            "jsonld" or "json-ld" or "json" =>
                new JsonLdRdfWriter(settings, prefixMappings, uriMappings),
            "ram" or "test" => new RamRdfWriter(settings, prefixMappings, uriMappings),
            _ => throw new NotSupportedException($"RDF format '{format}' " +
                $"is not supported")
        };
    }
}
