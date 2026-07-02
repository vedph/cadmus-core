using System;

namespace Cadmus.Philology.Tools;

/// <summary>
/// A parsing exception.
/// </summary>
public class ParseException : Exception
{
    /// <summary>
    /// Gets the substring of the input that is related to the error.
    /// </summary>
    public string InputSubstring { get; }

    /// <summary>
    /// Gets the error position. A value of -1 means unspecified.
    /// </summary>
    public int Position { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParseException"/> class
    /// with a specified error message, the input substring that caused the error,
    /// and an optional position within the input.
    /// </summary>
    /// <param name="message">The error message that explains the reason for
    /// the exception.</param>
    /// <param name="inputSubstring">The substring of the input that caused the
    /// parsing error.</param>
    /// <param name="position">The zero-based position within the input where the
    /// error occurred. The default value is -1, indicating that the position
    /// is unspecified.</param>
    public ParseException(string message, string inputSubstring, int position = -1)
        : base(message)
    {
        InputSubstring = inputSubstring;
        Position = position;
    }
}
