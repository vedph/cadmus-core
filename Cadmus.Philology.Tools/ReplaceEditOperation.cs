using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Replace edit operation.
/// </summary>
public sealed class ReplaceEditOperation : EditOperation
{
    /// <summary>
    /// Gets the type of operation.
    /// </summary>
    public override OperationType Type => OperationType.Replace;

    /// <summary>
    /// Gets or sets the replacement text.
    /// </summary>
    public string ReplacementText { get; set; } = string.Empty;

    /// <summary>
    /// Executes the operation on the given input text.
    /// </summary>
    /// <param name="input">The input text.</param>
    /// <returns>The text after the operation has been applied.</returns>
    public override string Execute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (Run < 1)
        {
            throw new ArgumentOutOfRangeException("position",
                $"Position {Run} is out of range a replace");

        }
        ValidatePosition(input, At, Run);
        StringBuilder result = new(input);
        result.Remove(At - 1, Run);
        result.Insert(At - 1, ReplacementText);
        return result.ToString();
    }

    /// <summary>
    /// Parses the specified text to populate the object's properties.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <exception cref="ArgumentNullException">text</exception>
    /// <exception cref="ParseException">Invalid format</exception>
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // pattern: "A"@NxN="B" or @NxN="B"
        string pattern = @"(?:""([^""]*)"")?\s*@(\d+)(?:[x×](\d+))?\s*=\s*""([^""]*)""";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid replace operation format. " +
                "Expected: \"oldtext\"@position=\"newtext\" " +
                "or @position=\"newtext\"", text);
        }

        InputText = match.Groups[1].Success ? match.Groups[1].Value : null;

        if (!int.TryParse(match.Groups[2].Value, out int position) || position < 1)
        {
            throw new ParseException("Position must be a positive integer",
                match.Groups[2].Value);
        }
        At = position;

        Run = 1;
        if (match.Groups[3].Success)
        {
            if (!int.TryParse(match.Groups[3].Value, out int length) || length < 1)
            {
                throw new ParseException("Length must be a positive integer",
                    match.Groups[3].Value);
            }
            Run = length;
        }

        ReplacementText = match.Groups[4].Value;
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
        sb.Append($"=\"{ReplacementText}\"");

        if (Tags.Count > 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
