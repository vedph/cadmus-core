using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Represents an operation that moves a specified substring within an input
/// string to a new position after the editing process. This operation is
/// defined by the starting position, length of the substring, and the target
/// position where the substring will be inserted.
/// </summary>
/// <remarks>The operation removes the specified substring from the input
/// string and inserts it at the target position. If the target position is
/// after the removed substring, the target position is adjusted to account
/// for the removal. The operation supports parsing from a string representation
/// and can generate a parsable string representation of itself.</remarks>
public sealed class MoveAfterEditOperation : EditOperation
{
    /// <summary>
    /// Gets the type of the operation represented by this instance.
    /// </summary>
    public override OperationType Type => OperationType.MoveAfter;

    /// <summary>
    /// Gets or sets the target position for the operation.
    /// </summary>
    public int To { get; set; }

    /// <summary>
    /// Moves a specified substring within the input string to a new position.
    /// </summary>
    /// <remarks>The substring to move is determined by the <c>Position</c> and
    /// <c>Length</c> properties, which specify the starting position (1-based)
    /// and the number of characters to move, respectively. The target position
    /// for the substring is specified by the <c>TargetPosition</c> property,
    /// which is also 1-based. If the target position is after the substring's
    /// original position, the target position is adjusted to account for the
    /// removal of the substring before insertion.</remarks>
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

        // remove the text first
        result.Remove(At - 1, Run);

        // adjust target position if it's after the removed text
        int adjustedTargetPosition = To;
        if (To > At) adjustedTargetPosition -= Run;

        // insert after target position
        result.Insert(adjustedTargetPosition, textToMove);

        return result.ToString();
    }

    /// <summary>
    /// Parses the specified text to extract and validate the parameters for a
    /// move-after operation.
    /// </summary>
    /// <param name="text">The input string representing the move-after operation.
    /// The expected format is either <c>"A"@N->@M</c> or <c>@N->@M</c>, where:
    /// <list type="bullet">
    /// <item><description><c>"A"</c> is an optional quoted string representing
    /// the input text.</description></item>
    /// <item><description><c>@N</c> is the starting position (a positive integer).
    /// </description></item> <item><description><c>@M</c> is the target
    /// position (a positive integer).</description></item>
    /// <item><description>An optional <c>xL</c> or <c>×L</c> specifies the
    /// length (a positive integer).</description></item>
    /// </list>
    /// </param>
    /// <exception cref="ParseException">Thrown if the input string does not
    /// match the expected format, or if any of the extracted values (position,
    /// length, or target position) are invalid.</exception>
    /// <exception cref="ArgumentNullException">text</exception>"
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // pattern: "A"@N->@M or @N->@M
        string pattern = @"(?:""([^""]*)"")?\s*@(\d+)(?:[x×](\d+))?\s*->\s*@(\d+)";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid move-after operation format. " +
                "Expected: \"text\"@position->@targetposition or " +
                "@position->@targetposition", text);
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

        if (!int.TryParse(match.Groups[4].Value, out int targetPosition)
            || targetPosition < 1)
        {
            throw new ParseException("Target position must be a positive " +
                "integer", match.Groups[4].Value);
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
        sb.Append($"->@{To}");

        if (Tags.Count != 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
