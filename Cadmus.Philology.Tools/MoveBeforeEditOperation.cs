using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Represents an operation that moves a specified substring within an input
/// string to a new position before performing an edit operation.
/// </summary>
/// <remarks>This operation extracts a substring from the input string,
/// defined by its starting position and length, and inserts it at a specified
/// target position. The operation adjusts the target position if it is
/// affected by the removal of the substring. The operation is reversible
/// and can be parsed from or serialized to a string representation.</remarks>
public sealed class MoveBeforeEditOperation : EditOperation
{
    /// <summary>
    /// Gets the type of the operation represented by this instance.
    /// </summary>
    public override OperationType Type => OperationType.MoveBefore;

    /// <summary>
    /// Gets or sets the target position value.
    /// </summary>
    public int To { get; set; }

    /// <summary>
    /// Moves a specified substring within the input string to a new position.
    /// </summary>
    /// <remarks>The substring to move is determined by the <c>Position</c> and
    /// <c>Length</c> properties, which specify the starting position (1-based)
    /// and the number of characters to move, respectively. The substring
    /// is inserted at the position specified by the <c>TargetPosition</c>
    /// property, which is also 1-based. If <c>TargetPosition</c> is after the
    /// substring's original position, the target position is adjusted to account
    /// for the removal of the substring before insertion.</remarks>
    /// <param name="input">The input string from which the substring will be
    /// moved.</param>
    /// <returns>A new string with the specified substring moved to the target
    /// position.</returns>
    /// <exception cref="ArgumentNullException">input</exception>
    public override string Execute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        ValidatePosition(input, At, Run);
        ValidatePosition(input, To);

        string textToMove = input.Substring(At - 1, Run);
        StringBuilder result = new(input);

        // Remove the text first
        result.Remove(At - 1, Run);

        // Adjust target position if it's after the removed text
        int adjustedTargetPosition = To;
        if (To > At)
        {
            adjustedTargetPosition -= Run;
        }

        // Insert at target position
        result.Insert(adjustedTargetPosition - 1, textToMove);

        return result.ToString();
    }

    /// <summary>
    /// Parses the specified text to extract and validate the parameters for a
    /// move-before operation.
    /// </summary>
    /// <param name="text">The input string to parse. The expected format is
    /// either  "text"@position>@targetposition or @position>@targetposition,
    /// where: • "text" is an optional quoted string. • position is the starting
    /// position (a positive integer). • targetposition is the target position
    /// (a positive integer). • An optional x or × followed by a length
    /// (a positive integer) may be included after position.</param>
    /// <exception cref="ParseException">Thrown if the input string does not
    /// match the expected format, or if any of the extracted values (position,
    /// length, or target position) are invalid.</exception>
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // Pattern: "A"@N>@M or @N>@M
        string pattern = @"(?:""([^""]*)"")?\s*@(\d+)(?:[x×](\d+))?\s*>\s*@(\d+)";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid move-before operation format. " +
                "Expected: \"text\"@position>@targetposition or " +
                "@position>@targetposition", text);
        }

        InputText = match.Groups[1].Success ? match.Groups[1].Value : null;

        if (!int.TryParse(match.Groups[2].Value, out int position)
            || position < 1)
        {
            throw new ParseException("Position must be a positive integer",
                match.Groups[2].Value);
        }
        At = position;

        Run = 1;
        if (match.Groups[3].Success)
        {
            if (!int.TryParse(match.Groups[3].Value, out int length)
                || length < 1)
            {
                throw new ParseException("Length must be a positive integer",
                    match.Groups[3].Value);
            }
            Run = length;
        }

        if (!int.TryParse(match.Groups[4].Value, out int targetPosition)
            || targetPosition < 1)
        {
            throw new ParseException("Target position must be a positive integer",
                match.Groups[4].Value);
        }
        To = targetPosition;

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
        sb.Append($">@{To}");

        if (Tags.Count > 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
