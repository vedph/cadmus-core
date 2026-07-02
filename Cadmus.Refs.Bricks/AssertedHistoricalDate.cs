using Fusi.Antiquity.Chronology;
using System;

namespace Cadmus.Refs.Bricks;

/// <summary>
/// A <see cref="HistoricalDate"/> with an <see cref="Assertion"/>.
/// </summary>
public class AssertedHistoricalDate : HistoricalDate, IHasAssertion
{
    /// <summary>
    /// Gets or sets the optional tag. This is an arbitrary value used
    /// to classify or group dates.
    /// </summary>
    public string? Tag { get; set; }

    /// <summary>
    /// Gets or sets the assertion.
    /// </summary>
    public Assertion? Assertion { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertedHistoricalDate"/> class.
    /// </summary>
    public AssertedHistoricalDate()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AssertedHistoricalDate"/> class.
    /// </summary>
    /// <param name="date">The date to set for this asserted date.</param>
    /// <exception cref="ArgumentNullException">date</exception>
    public AssertedHistoricalDate(HistoricalDate date)
    {
        ArgumentNullException.ThrowIfNull(date);

        A = date.A;
        B = date.B;
    }

    /// <summary>
    /// Parses a string into an <see cref="AssertedHistoricalDate"/>.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <returns>Parsed date or null.</returns>
    public static AssertedHistoricalDate? Parse(string text)
    {
        HistoricalDate? date = HistoricalDate.Parse(text);
        return date is null ? null : new AssertedHistoricalDate(date);
    }
}
