using Fusi.Tools;
using Fusi.Tools.Configuration;
using Proteus.Core.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Export.Filters;

/// <summary>
/// BCP-47 language tag filter. This is a simple lookup filter replacing
/// BCP-47 language tags with the corresponding English language names.
/// BCP-47 uses variable-length codes (e.g., 'el' for Modern Greek,
/// 'grc' for Ancient Greek). Custom tags can be mapped using a dictionary,
/// with fallback to the tag itself when no match is found.
/// <para>Tag: <c>it.vedph.text-filter.str.bcp47</c>.</para>
/// </summary>
[Tag("it.vedph.text-filter.str.bcp47")]
public sealed class Bcp47TextFilter : TextFilter<string>,
    IConfigurable<Bcp47FilterOptions>
{
    private static Dictionary<string, string>? _codes;
    private Regex _bcp47Regex;
    private Dictionary<string, string>? _customTagNames;

    /// <summary>
    /// Initializes a new instance of the <see cref="Bcp47TextFilter"/>
    /// class.
    /// </summary>
    public Bcp47TextFilter()
    {
        // match BCP-47 tags: primary language (2-3 letters) optionally
        // followed by subtags (hyphen + alphanumeric).
        // Examples: en, en-US, en-US-custom
        _bcp47Regex = new Regex(@"\^\^([a-z]{2,3}(?:-[a-zA-Z0-9]+)*)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(Bcp47FilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _bcp47Regex = new Regex(options.Pattern, RegexOptions.Compiled);

        // make custom tags case-insensitive
        if (options.CustomTagNames != null)
        {
            _customTagNames = new Dictionary<string, string>(
                options.CustomTagNames,
                StringComparer.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Loads the BCP-47 codes from the embedded resource file.
    /// </summary>
    private static void LoadCodes()
    {
        if (_codes != null) _codes.Clear();
        else _codes = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);

        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Cadmus.Export.Assets.Bcp47.txt")!,
            Encoding.UTF8);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            ReadOnlySpan<char> lineSpan = line.AsSpan();
            int commaIndex = lineSpan.IndexOf(',');
            if (commaIndex > 0 && commaIndex < lineSpan.Length - 1)
            {
                string code = lineSpan[..commaIndex].ToString();
                string name = lineSpan[(commaIndex + 1)..].ToString();
                _codes[code] = name;
            }
        }
    }

    /// <summary>
    /// Resolves a BCP-47 language code to its name.
    /// </summary>
    /// <param name="code">The BCP-47 code.</param>
    /// <returns>The language name, or the code itself if not found.
    /// </returns>
    private string ResolveCode(ReadOnlySpan<char> code)
    {
        string codeStr = code.ToString();

        // first try custom tags if provided
        if (_customTagNames?.TryGetValue(codeStr, out string? customName)
            == true)
        {
            return customName;
        }

        // then try standard BCP-47 codes
        if (_codes!.TryGetValue(codeStr, out string? standardName))
        {
            return standardName;
        }

        // fall back to the code itself
        return codeStr;
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
        if (string.IsNullOrEmpty(text) || _bcp47Regex == null) return text;

        if (_codes == null) LoadCodes();

        return _bcp47Regex.Replace(text, (Match m) =>
        {
            ReadOnlySpan<char> code = m.Groups[1].ValueSpan;
            return ResolveCode(code);
        });
    }
}

/// <summary>
/// Options for <see cref="Bcp47TextFilter"/>.
/// </summary>
public class Bcp47FilterOptions
{
    /// <summary>
    /// Gets or sets the pattern used to identify BCP-47 codes.
    /// It is assumed that the code is the first captured group in a match.
    /// Default is <c>^^</c> followed by 2-3 lowercase letters, optionally
    /// followed by subtags (hyphen + alphanumeric).
    /// Examples: ^^en, ^^en-US, ^^en-US-custom.
    /// </summary>
    public string Pattern { get; set; } =
        @"\^\^([a-z]{2,3}(?:-[a-zA-Z0-9]+)*)";

    /// <summary>
    /// Gets or sets the custom tag names dictionary. This allows mapping
    /// custom BCP-47 tags to their display names. When a tag is not found
    /// in the standard BCP-47 codes, the filter will check this dictionary.
    /// If still not found, it falls back to the tag itself.
    /// </summary>
    public Dictionary<string, string>? CustomTagNames { get; set; }
}
