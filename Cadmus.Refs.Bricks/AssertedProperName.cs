using System;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A <see cref="ProperName"/> with an <see cref="Assertion"/>.
/// </summary>
public class AssertedProperName : ProperName, IHasAssertion
{
    /// <summary>
    /// Gets or sets the assertion.
    /// </summary>
    public Assertion? Assertion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertedProperName"/>
    /// class.
    /// </summary>
    public AssertedProperName()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertedProperName"/>
    /// class.
    /// </summary>
    /// <param name="name">The name to set this asserted name to.</param>
    /// <exception cref="ArgumentNullException">name</exception>
    public AssertedProperName(ProperName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        Language = name.Language;
        Tag = name.Tag;
        Pieces = name.Pieces;
    }
}
