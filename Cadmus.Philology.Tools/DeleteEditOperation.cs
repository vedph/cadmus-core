using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Represents an operation that deletes a specified number of characters
/// from a given position in a string.
/// </summary>
/// <remarks>The <see cref="DeleteEditOperation"/> class is a specialized edit
/// operation that removes a substring from the input string based on the
/// specified position and length. The operation can be defined using a DSL
/// format, which is parsed to determine the position and length of the deletion.
/// This class is immutable and thread-safe.</remarks>
public sealed class DeleteEditOperation : EditOperation
{
    public override OperationType Type => OperationType.Delete;

    /// <summary>
    /// Removes a specified number of characters from the input string,
    /// starting at a given position.
    /// </summary>
    /// <param name="input">The string from which characters will be removed.</param>
    /// <returns>A new string with the specified characters removed.</returns>
    public override string Execute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidatePosition(input, At, Run);
        return input.Remove(At - 1, Run);
    }

    /// <summary>
    /// Parses the specified DSL text to extract the input text, position,
    /// and length for a delete operation.
    /// </summary>
    /// <param name="text">The DSL text to parse. The expected format is
    /// <c>"text"@position!</c> or <c>@position!</c>, where <c>position</c> is
    /// a positive integer and an optional <c>length</c> can be specified as
    /// <c>@positionxlength!</c>.</param>
    /// <exception cref="ParseException">Thrown if the DSL text does not match
    /// the expected format, if the position is not a positive integer, or if the
    /// length (if specified) is not a positive integer.</exception>
    /// <exception cref="ArgumentNullException">text</exception>
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // pattern: "A"@NxN! or @NxN!
        string pattern = @"(?:""([^""]*)"")?\s*@(\d+)(?:[x×](\d+))?\s*!";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid delete operation format. " +
                "Expected: \"text\"@position! or @position!", text);
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

        ParseNoteAndTags(text);
    }

    /// <summary>
    /// Returns a parsable string representation of the object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        StringBuilder sb = new();

        if (!string.IsNullOrEmpty(InputText))
            sb.Append($"\"{InputText}\"");

        sb.Append($"@{At}");
        if (Run > 1) sb.Append($"x{Run}");
        sb.Append('!');

        if (Tags.Count != 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
