namespace Cadmus.Export.Rdf;

/// <summary>
/// Statistics about RDF writer output.
/// Used by the RAM-based writer.
/// </summary>
public class RdfWriterStatistics
{
    /// <summary>
    /// The total number of triples written.
    /// </summary>
    public int TripleCount { get; set; }

    /// <summary>
    /// The number of triples with literal objects.
    /// </summary>
    public int LiteralTripleCount { get; set; }

    /// <summary>
    /// The number of triples with resource (URI) objects.
    /// </summary>
    public int ResourceTripleCount { get; set; }

    /// <summary>
    /// The number of unique subjects.
    /// </summary>
    public int UniqueSubjects { get; set; }

    /// <summary>
    /// The number of unique predicates.
    /// </summary>
    public int UniquePredicates { get; set; }

    /// <summary>
    /// The number of unique objects.
    /// </summary>
    public int UniqueObjects { get; set; }

    /// <summary>
    /// The number of header lines written.
    /// </summary>
    public int HeaderLineCount { get; set; }

    /// <summary>
    /// The number of footer lines written.
    /// </summary>
    public int FooterLineCount { get; set; }

    /// <summary>
    /// The number of output lines written.
    /// </summary>
    public int OutputLineCount { get; set; }
}
