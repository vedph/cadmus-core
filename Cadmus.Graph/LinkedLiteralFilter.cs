namespace Cadmus.Graph;

/// <summary>
/// A literal filter that is linked to a specific triple, by subject and
/// predicate identifiers.
/// </summary>
public class LinkedLiteralFilter : LiteralFilter
{
    /// <summary>
    /// Gets or sets the subject identifier in the triple including the
    /// literal to match.
    /// </summary>
    public int SubjectId { get; set; }

    /// <summary>
    /// Gets or sets the property identifier in the triple including the
    /// literal to match.
    /// </summary>
    public int PredicateId { get; set; }
}
