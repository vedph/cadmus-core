using System;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Represents an edit operation that inserts a specified text before a given
/// position in the input string.
/// </summary>
/// <remarks>This operation inserts the <see cref="Text"/> at the
/// specified <c>Position</c> in the input string. If the position is 0, the
/// text is inserted at the beginning of the string. The position must be within
/// the bounds of the input string, or an exception will be thrown. The
/// operation can be parsed from a DSL string in the format
/// <c>@position+="text"</c>.</remarks>
public sealed class InsertBeforeEditOperation : EditOperation
{
    /// <summary>
    /// Gets the type of the operation represented by this instance.
    /// </summary>
    public override OperationType Type => OperationType.InsertBefore;

    /// <summary>
    /// Gets or sets the text to insert.
    /// </summary>
    public string Text { get; set; } = "";

    /// <summary>
    /// Executes the operation by inserting or appending text to the specified
    /// input string.
    /// </summary>
    /// <remarks>If the position is zero, the text is appended to the input
    /// string. Otherwise, the text is inserted at the specified position
    /// (1-based index). Ensure the position is within the valid range of the
    /// input string.</remarks>
    /// <param name="input">The input string to which the operation will be
    /// applied.</param>
    /// <returns>A new string with the text inserted at the specified position,
    /// or appended if the position is zero.</returns>
    public override string Execute(string input)
    {
        if (At == 0) return Text + input;

        ValidatePosition(input, At);
        return input.Insert(At - 1, Text);
    }

    /// <summary>
    /// Parses the specified text to extract the position and text for an
    /// insert-before operation.
    /// </summary>
    /// <remarks>This method extracts the position and text from the input
    /// string and assigns them to the <c>Position</c> and <c>Text</c> properties,
    /// respectively. Additional parsing for notes and tags is performed by
    /// the <c>ParseNoteAndTags</c> method.</remarks>
    /// <param name="text">The input string to parse. The string must follow 
    /// the format <c>@position+="text"</c>, where <c>position</c> is a
    /// non-negative integer and <c>text</c> is the content to insert.</param>
    /// <exception cref="ParseException">Thrown if the input string does not 
    /// match the expected format or if the position is not a non-negative 
    /// integer.</exception>
    public override void Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        // pattern: @N+="B"
        string pattern = @"@(\d+)\s*\+=\s*""([^""]*)""";
        Match match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);

        if (!match.Success)
        {
            throw new ParseException("Invalid insert-before operation format. " +
                "Expected: @position+=\"text\"", text);
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
        sb.Append($"@{At}+=\"{Text}\"");

        if (Tags.Count != 0) sb.Append($" [{string.Join(" ", Tags)}]");

        if (!string.IsNullOrEmpty(Note)) sb.Append($" {{{Note}}}");

        return sb.ToString();
    }
}
