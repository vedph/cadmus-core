using Markdig;
using System;
using System.Text;

namespace Cadmus.Export;

/// <summary>
/// Markdown helper. This is used by those processors requiring to handle
/// Markdown code included between some arbitrarily defined tags.
/// </summary>
public static class MarkdownHelper
{
    /// <summary>
    /// Converts all the Markdown regions found in source.
    /// </summary>
    /// <param name="source">The source text.</param>
    /// <param name="open">The Markdown open tag.</param>
    /// <param name="close">The Markdown close tag.</param>
    /// <param name="plain">if set to <c>true</c>, convert to plain text;
    /// else convert to HTML.</param>
    /// <returns>Converted text.</returns>
    /// <exception cref="ArgumentNullException">source or open or close
    /// </exception>
    public static string ConvertRegions(string source, string open,
        string close, bool plain)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(open);
        ArgumentNullException.ThrowIfNull(close);

        StringBuilder sb = new();
        int start = 0, i = source.IndexOf(open);
        while (i > -1)
        {
            // prepend head
            if (i > start) sb.Append(source, start, i - start);

            // skip open tag and find close
            i += open.Length;
            int j = source.IndexOf(close, i);
            // if not found, find next open; if not found, go up to end
            if (j == -1)
            {
                j = source.IndexOf(open, i);
                if (j == -1) j = source.Length;
            }

            // convert region
            string md = source[i..j];
            sb.Append(plain? Markdown.ToPlainText(md) : Markdown.ToHtml(md));

            // skip close tag and move to next open if any
            start = j + close.Length;
            if (start >= source.Length) break;
            i = source.IndexOf(open, start);
        }

        // append tail
        if (start < source.Length)
            sb.Append(source, start, source.Length - start);

        return sb.ToString();
    }
}
