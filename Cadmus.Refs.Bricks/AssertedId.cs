namespace Cadmus.Refs.Bricks;

/// <summary>
/// An <see cref="ExternalId"/> with an <see cref="Assertion"/>.
/// </summary>
public class AssertedId : ExternalId, IHasAssertion
{
    /// <summary>
    /// Gets or sets the assertion.
    /// </summary>
    public Assertion? Assertion { get; set; }
}
