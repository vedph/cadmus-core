using Fusi.Tools;
using Fusi.Tools.Configuration;
using Proteus.Core.Text;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Export.Filters;

/// <summary>
/// Trivial sentence split renderer filter. This is used to split a text
/// into sentences, so that each line corresponds to a single sentence.
/// Sentence splitting is performed on the basis of a list of end-of-sentence
/// markers.
/// <para>Tag: <c>it.vedph.text-filter.str.sentence-split</c>.
/// Old tag: <c>it.vedph.renderer-filter.sentence-split</c>.</para>
/// </summary>
[Tag("it.vedph.text-filter.str.sentence-split")]
public sealed class SentenceSplitTextFilter : TextFilter<string>,
    IConfigurable<SentenceSplitRendererFilterOptions>
{
    private readonly HashSet<char> _markers;
    private readonly Regex _crLfRegex;
    private string _newLine;
    private bool _trimming;
    private bool _crLfRemoval;
    private char[] _blackOpeners;
    private char[] _blackClosers;
    private bool _inBlack;

    /// <summary>
    /// Initializes a new instance of the <see cref="SentenceSplitTextFilter"/>
    /// class.
    /// </summary>
    public SentenceSplitTextFilter()
    {
        _crLfRegex = new(@"\r?\n", RegexOptions.Compiled);
        _markers =
        [
            '.', '?', '!',
            '\u037e',   // Greek ';'
            '\u2026'    // ellipsis
        ];
        _newLine = Environment.NewLine;
        _blackOpeners = ['('];
        _blackClosers = [')'];
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(SentenceSplitRendererFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!string.IsNullOrEmpty(options.EndMarkers))
        {
            _markers.Clear();
            foreach (char c in options.EndMarkers) _markers.Add(c);
        }
        if (!string.IsNullOrEmpty(options.BlackOpeners))
            _blackOpeners = options.BlackOpeners.ToCharArray();

        if (!string.IsNullOrEmpty(options.BlackClosers))
            _blackClosers = options.BlackClosers.ToCharArray();

        _newLine = options.NewLine;
        _trimming = options.Trimming;
        _crLfRemoval = options.CrLfRemoval;
    }

    private int LocateNextSeparator(string text, int start = 0)
    {
        // this method was borrowed from Chiron.Core
        if (start >= text.Length) return -1;
        int i = start;

        do
        {
            // if we're in a black section, just look for its end
            if (_inBlack)
            {
                i = text.IndexOfAny(_blackClosers, i);
                if (i == -1) return -1;
                i++;    // skip section closer
            }

            // skip initial markers, which would produce an empty output
            while (i < text.Length && _markers.Contains(text[i])) i++;
            if (i == text.Length) return -1;

            // locate next marker from here:
            while (i < text.Length)
            {
                // if it's a black section opener, enter the section and retry
                if (Array.IndexOf(_blackOpeners, text[i]) > -1)
                {
                    _inBlack = true;
                    i++;    // skip the opener
                    break;
                }

                // if it's an end marker, return its location ensuring to
                // place sentence end at the last one of a sequence
                if (_markers.Contains(text[i]))
                {
                    // go past other markers next to the one just found
                    while (i + 1 < text.Length &&
                           (_markers.Contains(text[i + 1]) ||
                            char.IsWhiteSpace(text[i + 1])))
                    {
                        i++;
                    }
                    return i;
                }

                // else keep searching
                i++;
            }
        } while (_inBlack);
        return -1;
    }

    private void TrimAroundNewLine(StringBuilder text, int index)
    {
        int a, b;

        // right
        a = b = index + _newLine.Length;
        while (b < text.Length && (text[b] == ' ' || text[b] == '\t')) b++;
        if (b > a) text.Remove(a, b - a);

        // left
        a = b = index;
        while (a > 0 && (text[a - 1] == ' ' || text[a - 1] == '\t')) a--;
        if (a < b) text.Remove(a, b - a);
    }

    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="context">The optional context.</param>
    /// <returns>Filtered text or null.</returns>
    protected override object? DoApply(string? text,
        IHasDataDictionary? context = null)
    {
        if (string.IsNullOrEmpty(text)) return text;

        if (_crLfRemoval) text = _crLfRegex.Replace(text, " ");

        StringBuilder sb = new();
        List<int>? nlIndexes = _trimming ? new() : null;
        int start = 0, index = LocateNextSeparator(text);

        while (index > -1)
        {
            if (index > start) sb.Append(text, start, index + 1 - start);
            nlIndexes?.Add(sb.Length);
            sb.Append(_newLine);
            start = ++index;
            index = LocateNextSeparator(text, index);
        }
        if (start < text.Length)
        {
            sb.Append(text, start, text.Length - start);
            nlIndexes?.Add(sb.Length);
            sb.Append(_newLine);
        }

        if (_trimming)
        {
            for (int j = nlIndexes!.Count - 1; j > -1; j--)
                TrimAroundNewLine(sb, nlIndexes[j]);
        }

        return sb.ToString();
    }
}

/// <summary>
/// Options for <see cref="SentenceSplitTextFilter"/>.
/// </summary>
public class SentenceSplitRendererFilterOptions
{
    /// <summary>
    /// Gets or sets the end-of-sentence marker characters. Each character
    /// in this string is treated as a sentence end marker. Any sequence
    /// of such end marker characters is treated as a single end.
    /// Default characters are <c>.</c>, <c>?</c>, <c>!</c>, Greek question
    /// mark (U+037E), and ellipsis (U+2026).
    /// </summary>
    public string EndMarkers { get; set; } = ".?!\u037e\u2026";

    /// <summary>
    /// Gets or sets the "black" section openers characters. Each character
    /// in this string has a corresponding closing character in
    /// <see cref="BlackClosers"/>, and marks the beginning of a section
    /// which may contain end markers which will not count as sentence
    /// separators. This is typically used for parentheses, e.g. "hoc tibi
    /// dico (cui enim?) ut sapias", where we do not want the sentence to
    /// stop after the question mark.
    /// These sections cannot be nested, so you are free to use the same
    /// character both as an opener and as a closer, e.g. an EM dash.
    /// The default value is <c>(</c>. If no such sections must be detected,
    /// just leave this null/empty.
    /// </summary>
    public string? BlackOpeners { get; set; } = "(";

    /// <summary>
    /// Gets or sets the "black" section closers. Each character in this
    /// string has a corresponding opening character in
    /// <see cref="BlackOpeners"/>. The default value is <c>)</c>.
    /// If no such sections must be detected, just leave this null/empty.
    /// </summary>
    public string? BlackClosers { get; set; } = ")";

    /// <summary>
    /// Gets or sets the newline marker to use. The default value is the
    /// newline sequence of the host OS.
    /// </summary>
    public string NewLine { get; set; } = Environment.NewLine;

    /// <summary>
    /// Gets or sets a value indicating whether trimming spaces/tabs at
    /// both sides of any inserted newline is enabled.
    /// </summary>
    public bool Trimming { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether CR/LF should be removed
    /// when filtering. When this is true, any CR or CR+LF or LF is replaced
    /// with a space.
    /// </summary>
    public bool CrLfRemoval { get; set; }
}
