namespace Cadmus.Export.Rdf;

/// <summary>
/// Represents an RDF triple.
/// </summary>
public class RdfTriple
{
    /// <summary>
    /// The numeric ID of the triple in the source database.
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// The numeric ID of the subject node.
    /// </summary>
    public int SubjectId { get; set; }

    /// <summary>
    /// The numeric ID of the predicate node.
    /// </summary>
    public int PredicateId { get; set; }

    /// <summary>
    /// The numeric ID of the object node, if the object is a URI;
    /// </summary>
    public int? ObjectId { get; set; }

    /// <summary>
    /// The literal value of the object node, if the object is a literal.
    /// </summary>
    public string? ObjectLiteral { get; set; }

    /// <summary>
    /// The type of the object literal, if applicable. This corresponds to
    /// literal suffixes after <c>^^</c> in Turtle: e.g. <c>"12.3"^^xs:double</c>.
    /// </summary>
    public string? ObjectLiteralType { get; set; }

    /// <summary>
    /// The language of the object literal, if applicable. This is meaningful
    /// only for string literals, and usually is a BCP47 or ISO639 code.
    /// </summary>
    public string? ObjectLiteralLanguage { get; set; }

    /// <summary>
    /// Gets or sets an optional tag associated with the object.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// String representation of the object.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{Id}: {SubjectId} {PredicateId} " +
            (ObjectId.HasValue
            ? ObjectId.ToString()
            : $"\"{ObjectLiteral}\"{ObjectLiteralLanguage}");
    }
}
