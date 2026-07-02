using System;
using System.Collections.Generic;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Utility class for URI operations.
/// </summary>
public static class UriHelper
{
    /// <summary>
    /// Expands a short URI (prefix:localName) to its full form using the
    /// provided prefix mappings.
    /// </summary>
    /// <param name="shortUri">The short URI.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <returns>Expanded URI.</returns>
    public static string ExpandUri(string shortUri,
        Dictionary<string, string> prefixMappings)
    {
        if (string.IsNullOrEmpty(shortUri) || !shortUri.Contains(':'))
            return shortUri;

        int colonIndex = shortUri.IndexOf(':');
        string prefix = shortUri[..colonIndex];
        string localName = shortUri[(colonIndex + 1)..];

        if (prefixMappings.TryGetValue(prefix, out string? namespaceUri))
            return namespaceUri + localName;

        return shortUri;
    }

    /// <summary>
    /// True if the given URI is valid (absolute or relative).
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>True if valid.</returns>
    public static bool IsValidUri(string uri)
    {
        if (string.IsNullOrEmpty(uri))
            return false;

        return Uri.TryCreate(uri, UriKind.Absolute, out _) ||
               Uri.TryCreate(uri, UriKind.Relative, out _);
    }

    /// <summary>
    /// Escapes the given URI string.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The escaped URI.</returns>
    public static string EscapeUri(string uri)
    {
        if (string.IsNullOrEmpty(uri)) return uri;
        return Uri.EscapeDataString(uri);
    }
}
