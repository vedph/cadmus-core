using System.Collections.Generic;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// Asserted composite (internal/external) identifier.
/// </summary>
public class AssertedCompositeId : IHasAssertion
{
    /// <summary>
    /// Gets or sets the pin-based target ID.
    /// </summary>
    public PinTarget? Target { get; set; }

    /// <summary>
    /// Gets or sets the optional scope.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Gets or sets an optional tag.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets an optional list of features associated with the object.
    /// These usually derive from a thesaurus and can be used to connotate
    /// the type of the link.
    /// </summary>
    public List<string>? Features { get; set; }

    /// <summary>
    /// An optional generic note about the link.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    /// Gets or sets the optional assertion.
    /// </summary>
    public Assertion? Assertion { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        return Target?.ToString() ?? "";
    }
}
