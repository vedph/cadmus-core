using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Represents an operation that inserts a specified text at a given position
/// in the input string.
/// </summary>
/// <remarks>This operation appends the <see cref="Text"/> to the
/// end of the input string if the position is zero. Otherwise, it inserts
/// the <see cref="Text"/> at the specified position in the input
/// string. The position must be within the bounds of the input string,
/// or an exception will be thrown.</remarks>
public sealed class InsertAfterEditOperation : EditOperation
{
    /// <summary>
    /// Gets the type of the operation represented by this instance.
    /// </summary>
    public override OperationType Type => OperationType.InsertAfter;

    /// <summary>
    /// Gets or sets the text to be inserted.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Inserts the specified text into the input string at the defined position
    /// or appends it if the position is zero.
    /// </summary>
    /// <param name="input">The string into which the text will be inserted or
    /// appended.</param>
    /// <returns>A new string with the specified text inserted at the defined
    /// position or appended if the position is zero.</returns>
    /// <exception cref="ArgumentNullException">input</exception>
    public override string Execute(string input)
    {
        ArgumentNullException.ThrowIfNull(input);

        if (At == 0) return input + Text;

        ValidatePosition(input, At);
        return input.Insert(At, Text);
    }

    /// <summary>
    /// Parses the specified text to extract the position and text for an
    /// insert-after operation.
    /// </summary>
    /// <remarks>This method extracts the position and text from the input
    /// string and assigns them to the <see cref="Position"/> and
    /// <see cref="Text"/> properties, respectively. It also processes 
    /// additional notes and tags using the <c>ParseNoteAndTags</c> method.
    /// </remarks>
    /// <param name="text">The input string to parse. The string must follow
    /// the format <c>@position=+"text"</c>, where <c>position</c> is a
    /// non-negative integer and  <c>text</c> is the content to insert.</param>
    /// <exception cref="ParseException">Thrown if the input string does not
    /// match the expected format, if the position is not a non-negative integer,
    /// or if other parsing errors occur.</exception>
    /// <exception cref="ArgumentNullException">text</exception>"
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // pattern: @N=+"B"
        string pattern = @"@(\d+)\s*=\+\s*""([^""]*)""";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid insert-after operation format. " +
                "Expected: @position=+\"text\"", text);
        }

        if (!int.TryParse(match.Groups[1].Value, out int position) || position < 0)
        {
            throw new ParseException("Position must be a non-negative integer", 
                match.Groups[1].Value);
        }
        At = position;

        Text = match.Groups[2].Value;
        ParseNoteAndTags(text);
    }

    /// <summary>
    /// Returns a parsable string representation of the object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        sb.Append($"@{At}=+\"{Text}\"");

        if (Tags.Count != 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
