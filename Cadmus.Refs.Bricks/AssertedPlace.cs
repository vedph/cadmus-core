namespace Cadmus.Refs.Bricks;

/// <summary>
/// A place with an <see cref="Assertion"/>.
/// </summary>
public class AssertedPlace : IHasAssertion
{
    /// <summary>
    /// Gets or sets the optional tag. This is an arbitrary value used
    /// to classify or group places.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Place value: usually this is an ID or a conventional name.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// An optional assertion.
    /// </summary>
    public Assertion? Assertion { get; set; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return Value ?? base.ToString()!;
    }
}
