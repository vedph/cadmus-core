using System;

namespace Cadmus.Index.PgSql;

/// <summary>
/// Helpers for preparing values for the PostgreSQL index.
/// </summary>
internal static class PgSqlText
{
    /// <summary>
    /// Adapts the specified text to a column: removes NUL characters (not
    /// allowed in PostgreSQL text) and truncates it to the maximum length
    /// without splitting surrogate pairs.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns>Adapted text, or empty if <paramref name="text"/> is null.
    /// </returns>
    public static string Fit(string? text, int maxLength)
    {
        if (string.IsNullOrEmpty(text)) return "";
        if (text.Contains('\0')) text = text.Replace("\0", "");
        if (text.Length <= maxLength) return text;

        int length = maxLength;
        if (char.IsHighSurrogate(text[length - 1])) length--;
        return text[..length];
    }

    /// <summary>
    /// Like <see cref="Fit(string?, int)"/>, but returns null for null.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="maxLength">The maximum length.</param>
    /// <returns>Adapted text or null.</returns>
    public static string? FitOrNull(string? text, int maxLength) =>
        text == null ? null : Fit(text, maxLength);

    /// <summary>
    /// Converts the specified date and time to UTC, assuming UTC when
    /// its kind is unspecified.
    /// </summary>
    /// <param name="dt">The date and time.</param>
    /// <returns>UTC date and time.</returns>
    public static DateTime ToUtc(DateTime dt) => dt.Kind switch
    {
        DateTimeKind.Utc => dt,
        DateTimeKind.Local => dt.ToUniversalTime(),
        _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
    };

    /// <summary>
    /// Quotes the specified SQL identifier.
    /// </summary>
    /// <param name="identifier">The identifier.</param>
    /// <returns>Quoted identifier.</returns>
    public static string QuoteIdentifier(string identifier) =>
        "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
