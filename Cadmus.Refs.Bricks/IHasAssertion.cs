namespace Cadmus.Refs.Bricks;

/// <summary>
/// Interface implemented by any object having an attached <see cref="Assertion"/>.
/// </summary>
public interface IHasAssertion
{
    /// <summary>
    /// Gets or sets the assertion.
    /// </summary>
    Assertion? Assertion { get; set; }
}
