using System;

namespace Cadmus.Index;

/// <summary>
/// Cadmus index query exception. This is thrown when a query is invalid,
/// either because of its syntax or because of its values (e.g. an invalid
/// regular expression).
/// </summary>
/// <seealso cref="Exception" />
public class CadmusQueryException : Exception
{
    /// <summary>
    /// Gets the 0-based position in the query text where the error was
    /// detected, or -1 if not applicable.
    /// </summary>
    public int Position { get; } = -1;

    /// <summary>
    /// Default constructor.
    /// </summary>
    public CadmusQueryException()
    {
    }

    /// <summary>
    /// Create a new exception with the specified error message.
    /// </summary>
    /// <param name="message">error message</param>
    public CadmusQueryException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Create a new exception with the specified error message and
    /// position.
    /// </summary>
    /// <param name="message">error message</param>
    /// <param name="position">The 0-based position in the query text.
    /// </param>
    public CadmusQueryException(string message, int position)
        : base(message)
    {
        Position = position;
    }

    /// <summary>
    /// Create a new exception with the specified error message and inner
    /// exception.
    /// </summary>
    /// <param name="message">error message</param>
    /// <param name="innerException">inner exception</param>
    public CadmusQueryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
