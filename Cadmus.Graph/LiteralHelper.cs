using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Graph;

/// <summary>
/// Helper for triple's literal value handling.
/// </summary>
public static partial class LiteralHelper
{
    // BCP47 language tag, e.g. en, en-US, zh-Hant-TW
    [GeneratedRegex("^[a-zA-Z]{1,8}(?:-[a-zA-Z0-9]{1,8})*$")]
    private static partial Regex GetLangRegex();

    /// <summary>
    /// Parses the specified literal in its Turtle-like forms: <c>"value"</c>,
    /// <c>"value"@lang</c>, <c>"value"^^type</c>. The language or type suffix
    /// is recognized only when it immediately follows the closing quote, so
    /// that quotes, <c>@</c> or <c>^^</c> inside the value are preserved.
    /// A literal not starting with a quote is returned as it is.
    /// </summary>
    /// <param name="literal">The literal.</param>
    /// <returns>Tuple with value, type (or null), and language (or null).
    /// </returns>
    /// <exception cref="ArgumentNullException">literal</exception>
    public static (string Value, string? Type, string? Language) ParseLiteral(
        string literal)
    {
        ArgumentNullException.ThrowIfNull(literal);

        if (literal.Length < 2 || literal[0] != '"') return (literal, null, null);

        // find the closing quote: the last one, unless it is followed by
        // a valid suffix (which cannot contain quotes)
        int q = literal.LastIndexOf('"');
        if (q == 0) return (literal[1..], null, null);

        string value = literal[1..q];
        string suffix = literal[(q + 1)..];
        if (suffix.Length == 0) return (value, null, null);

        if (suffix.StartsWith("^^", StringComparison.Ordinal) &&
            suffix.Length > 2 && !suffix.Any(char.IsWhiteSpace))
        {
            return (value, suffix[2..], null);
        }
        if (suffix[0] == '@' && GetLangRegex().IsMatch(suffix[1..]))
            return (value, null, suffix[1..]);

        // unrecognized suffix: keep the whole literal minus its opening quote
        return (literal[1..], null, null);
    }

    /// <summary>
    /// Converts the specified string value to boolean.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>True or false. False is returned also for invalid values.
    /// </returns>
    public static bool ConvertToBoolean(string? value)
    {
        return value?.ToLowerInvariant() switch
        {
            "1" => true,
            "0" => false,
            "true" => true,
            "false" => false,
            _ => false
        };
    }

    /// <summary>
    /// Parses the literal value of the specified triple, adjusting it
    /// accordingly.
    /// </summary>
    /// <param name="triple">The triple.</param>
    /// <exception cref="ArgumentNullException">triple</exception>
    public static void AdjustLiteral(Triple triple)
    {
        ArgumentNullException.ThrowIfNull(triple);

        if (triple.ObjectLiteral == null) return;

        // parse ^^type or @lang, removing them from the literal itself
        (string value, string? type, string? lang) =
            ParseLiteral(triple.ObjectLiteral);
        triple.ObjectLiteral = value;
        {
            // handle compatible numeric types
            if (type != null)
            {
                triple.LiteralType = type;
                // from https://docs.microsoft.com/en-us/dotnet/standard/data/xml/mapping-xml-data-types-to-clr-types
                switch (triple.LiteralType)
                {
                    case "xs:boolean":
                    case "xsd:boolean":
                        triple.LiteralNumber =
                            ConvertToBoolean(triple.ObjectLiteral) ? 1 : 0;
                        break;
                    case "xs:byte":
                    case "xsd:byte":
                    case "xs:short":
                    case "xsd:short":
                    case "xs:int":
                    case "xsd:int":
                    case "xs:integer":
                    case "xsd:integer":
                    case "xs:long":
                    case "xsd:long":
                    case "xs:float":
                    case "xsd:float":
                    case "xs:double":
                    case "xsd:double":
                    case "xs:unsignedByte":
                    case "xsd:unsignedByte":
                    case "xs:unsignedShort":
                    case "xsd:unsignedShort":
                    case "xs:unsignedInt":
                    case "xsd:unsignedInt":
                    case "xs:unsignedLong":
                    case "xsd:unsignedLong":
                        // a malformed numeric literal must not abort the
                        // whole mapping/import process
                        if (double.TryParse(triple.ObjectLiteral,
                            NumberStyles.Float | NumberStyles.AllowThousands,
                            CultureInfo.InvariantCulture, out double n))
                        {
                            triple.LiteralNumber = n;
                        }
                        break;
                }
            }
            else if (lang != null)
            {
                triple.LiteralLanguage = lang;
            }
        }

        // filter
        StringBuilder sb = new(triple.ObjectLiteral.Length);
        foreach (char c in triple.ObjectLiteral)
        {
            if (char.IsLetter(c)) sb.Append(UidFilter.GetSegment(c));
            else if (c == '\'' || char.IsDigit(c) || char.IsWhiteSpace(c))
                sb.Append(c);
        }
        triple.ObjectLiteralIx = sb.ToString();
    }
}
