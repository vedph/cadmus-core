using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Swap edit operation.
/// </summary>
public sealed class SwapEditOperation : EditOperation
{
    /// <summary>
    /// Gets or sets the type of the operation represented by this instance.
    /// </summary>
    public override OperationType Type => OperationType.Swap;

    /// <summary>
    /// The position of the second substring to swap (1-based).
    /// </summary>
    public int At2 { get; set; }

    /// <summary>
    /// The length of the second substring to swap.
    /// </summary>
    public int Run2 { get; set; } = 1;

    /// <summary>
    /// The optional input text associated with the second substring.
    /// </summary>
    public string? InputText2 { get; set; }

    /// <summary>
    /// Executes the swap operation on the given input string.
    /// </summary>
    /// <param name="input">The input string to modify.</param>
    /// <returns>The modified string after the swap operation.</returns>
    /// <exception cref="ArgumentNullException">input</exception>
    /// <exception cref="ArgumentException">Thrown when positions are invalid
    /// or overlap.</exception>
    public override string Execute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidatePosition(input, At, Run);
        ValidatePosition(input, At2, Run2);

        if (At == At2 || (At < At2 && At + Run > At2) ||
            (At2 < At && At2 + Run2 > At))
        {
            throw new ArgumentException("Swap positions cannot overlap");
        }

        string firstText = input.Substring(At - 1, Run);
        string secondText = input.Substring(At2 - 1, Run2);

        StringBuilder result = new(input);

        // replace in order of highest position first to avoid index shifting
        if (At > At2)
        {
            result.Remove(At - 1, Run);
            result.Insert(At - 1, secondText);
            result.Remove(At2 - 1, Run2);
            result.Insert(At2 - 1, firstText);
        }
        else
        {
            result.Remove(At2 - 1, Run2);
            result.Insert(At2 - 1, firstText);
            result.Remove(At - 1, Run);
            result.Insert(At - 1, secondText);
        }

        return result.ToString();
    }

    /// <summary>
    /// Parses the specified text to initialize the swap operation.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <exception cref="ArgumentNullException">text</exception>
    /// <exception cref="ParseException">Thrown when the text format is invalid.</exception>
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // pattern: "A"@NxN<>"B"@MxM or @NxN<>@MxM
        string pattern = @"(?:""([^""]*)"")?\s*@(\d+)(?:[x×](\d+))?\s*<>\s*(?:""([^""]*)"")?\s*@(\d+)(?:[x×](\d+))?";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid swap operation format. " +
                "Expected: \"text1\"@position1<>\"text2\"@position2", text);
        }

        InputText = match.Groups[1].Success ? match.Groups[1].Value : null;

        if (!int.TryParse(match.Groups[2].Value, out int position)
            || position < 1)
        {
            throw new ParseException("First position must be a positive integer", 
                match.Groups[2].Value);
        }
        At = position;

        Run = 1;
        if (match.Groups[3].Success)
        {
            if (!int.TryParse(match.Groups[3].Value, out int length) ||
                length < 1)
            {
                throw new ParseException("First length must be a positive integer",
                    match.Groups[3].Value);
            }
            Run = length;
        }

        InputText2 = match.Groups[4].Success ? match.Groups[4].Value : null;

        if (!int.TryParse(match.Groups[5].Value, out int secondPosition) ||
            secondPosition < 1)
        {
            throw new ParseException("Second position must be a positive integer",
                match.Groups[5].Value);
        }
        At2 = secondPosition;

        Run2 = 1;
        if (match.Groups[6].Success)
        {
            if (!int.TryParse(match.Groups[6].Value, out int secondLength) ||
                secondLength < 1)
            {
                throw new ParseException("Second length must be a positive integer",
                    match.Groups[6].Value);
            }
            Run2 = secondLength;
        }

        ParseNoteAndTags(text);
    }

    /// <summary>
    /// Returns a parsable string representation of the object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        StringBuilder sb = new();

        if (!string.IsNullOrEmpty(InputText)) sb.Append($"\"{InputText}\"");

        sb.Append($"@{At}");
        if (Run > 1) sb.Append($"x{Run}");
        sb.Append("<>");

        if (!string.IsNullOrEmpty(InputText2)) sb.Append($"\"{InputText2}\"");

        sb.Append($"@{At2}");
        if (Run2 > 1) sb.Append($"x{Run2}");

        if (Tags.Count > 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
