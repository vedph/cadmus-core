using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cadmus.Philology.Tools;

/// <summary>
/// Operation type.
/// </summary>
public enum OperationType
{
    Delete,
    InsertBefore,
    InsertAfter,
    Replace,
    MoveBefore,
    MoveAfter,
    Swap
}

/// <summary>
/// Base class for edit operations.
/// </summary>
public abstract class EditOperation
{
    protected static readonly Regex CoordinateRegex = new(
        @"@(\d+)(?:[x×](\d+))?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    protected static readonly Regex NoteRegex = new(
        @"\{([^)]*)\}",
        RegexOptions.Compiled);

    protected static readonly Regex TagsRegex = new(
        @"\[([^\]]*)\]",
        RegexOptions.Compiled);

    /// <summary>
    /// The type of operation.
    /// </summary>
    public abstract OperationType Type { get; }

    /// <summary>
    /// The input text for this operation.
    /// </summary>
    public string? InputText { get; set; }

    /// <summary>
    /// The start position (1-based) for this operation.
    /// </summary>
    public int At { get; set; }

    /// <summary>
    /// The length of text to be affected by this operation, starting from
    /// <see cref="At"/>. Default is 1.
    /// </summary>
    public int Run { get; set; } = 1;

    /// <summary>
    /// An optional note for this operation.
    /// </summary>
    public string Note { get; set; } = "";

    /// <summary>
    /// Optional tags for this operation.
    /// </summary>
    public List<string> Tags { get; set; } = [];

    /// <summary>
    /// Executes this operation on the given input string and returns
    /// the string transformed by this operation.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The output string.</returns>
    public abstract string Execute(string input);

    /// <summary>
    /// Parse the given DSL text and populate this operation's properties.
    /// </summary>
    /// <param name="text">The DSL text.</param>
    public abstract void Parse(string text);

    /// <summary>
    /// Parses coordinates in the format @N or @NxN from the given input string.
    /// </summary>
    /// <param name="text">The text to parse.</param>
    /// <returns>A tuple containing position and length.</returns>
    /// <exception cref="ParseException"></exception>
    protected static (int position, int length) ParseCoordinates(string text)
    {
        Match coordsMatch = CoordinateRegex.Match(text);
        if (!coordsMatch.Success)
        {
            throw new ParseException("Invalid coordinate format. " +
                "Expected @N or @NxN", text);
        }

        if (!int.TryParse(coordsMatch.Groups[1].Value, out int position) 
            || position < 1)
        {
            throw new ParseException("Position must be a positive integer",
                coordsMatch.Groups[1].Value);
        }

        int length = 1;
        if (coordsMatch.Groups[2].Success)
        {
            if (!int.TryParse(coordsMatch.Groups[2].Value, out length) 
                || length < 1)
            {
                throw new ParseException("Length must be a positive integer",
                    coordsMatch.Groups[2].Value);
            }
        }
        return (position, length);
    }

    /// <summary>
    /// Parses the specified input string to extract a note and associated tags.
    /// </summary>
    /// <remarks>This method uses regular expressions to identify and extract
    /// the note and tags from the input string. The extracted note is assigned
    /// to the <see cref="Note"/> property, and the tags are assigned to the
    /// <see cref="Tags"/> property as a list of strings. If no note or tags
    /// are found, the corresponding properties remain unchanged.</remarks>
    /// <param name="input">The input string containing the note and tags.
    /// The note and tags should be formatted according to the expected
    /// patterns defined by <see cref="NoteRegex"/> and
    /// <see cref="TagsRegex"/>.</param>
    protected void ParseNoteAndTags(string input)
    {
        Match noteMatch = NoteRegex.Match(input);
        if (noteMatch.Success)
        {
            Note = noteMatch.Groups[1].Value.Trim();
        }

        Match tagsMatch = TagsRegex.Match(input);
        if (tagsMatch.Success)
        {
            string tagsText = tagsMatch.Groups[1].Value.Trim();
            if (!string.IsNullOrEmpty(tagsText))
            {
                Tags = [.. tagsText.Split([' ', '\t'],
                    StringSplitOptions.RemoveEmptyEntries)];
            }
        }
    }

    /// <summary>
    /// Extracts the text enclosed in quotes from the specified input string
    /// based on the provided regular expression pattern.
    /// </summary>
    /// <param name="input">The input string to search for quoted text.</param>
    /// <param name="pattern">A regular expression pattern that defines the
    /// format of the quoted text.  The pattern must include a capturing
    /// group to extract the desired text.</param>
    /// <returns>The text captured by the first capturing group in the regular
    /// expression, or an empty string if no match is found.</returns>
    protected static string ExtractQuotedText(string input, string pattern)
    {
        Match match = Regex.Match(input, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return "";
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Validates that the specified position and length are within the bound
    /// of the given input string.
    /// </summary>
    /// <param name="input">The input string to validate against.</param>
    /// <param name="position">The starting position in the input string.
    /// Must be greater than 0 and less than or equal to the length of the
    /// string.</param>
    /// <param name="length">The number of characters to validate starting from
    /// the specified position. Defaults to 1. The range defined by
    /// <paramref name="position"/> and <paramref name="length"/> must not exceed
    /// the length of the input string.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if
    /// <paramref name="position"/> is less than 1, greater than the length of
    /// <paramref name="input"/>, or if the range defined by
    /// <paramref name="position"/> and <paramref name="length"/> exceeds the
    /// bounds of <paramref name="input"/>.</exception>
    protected static void ValidatePosition(string input, int position,
        int length = 1)
    {
        if (position < 1 || position > input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(position),
                $"Position {position} is out of range for input string " +
                $"of length {input.Length}");
        }
        if (position + length - 1 > input.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(length),
                $"Length {length} at position {position} exceeds input " +
                $"string length {input.Length}");
        }
    }

    /// <summary>
    /// Parse the given DSL text and return the corresponding operation.
    /// </summary>
    /// <param name="text">text</param>
    /// <returns>Operation</returns>
    /// <exception cref="ParseException"></exception>
    public static EditOperation ParseOperation(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ParseException("DSL text cannot be empty", text);
        }

        try
        {
            // determine operation type based on operators
            if (text.Contains('!'))
                return ParseTypedOperation<DeleteEditOperation>(text);
            else if (text.Contains("+="))
                return ParseTypedOperation<InsertBeforeEditOperation>(text);
            else if (text.Contains("=+"))
                return ParseTypedOperation<InsertAfterEditOperation>(text);
            else if (text.Contains("<>"))
                return ParseTypedOperation<SwapEditOperation>(text);
            else if (text.Contains("->"))
                return ParseTypedOperation<MoveAfterEditOperation>(text);
            else if (text.Contains('>'))
                return ParseTypedOperation<MoveBeforeEditOperation>(text);
            else if (text.Contains('='))
                return ParseTypedOperation<ReplaceEditOperation>(text);
            else
                throw new ParseException("Unknown operation type", text);
        }
        catch (ParseException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ParseException($"Error parsing operation: {ex.Message}", text);
        }
    }

    private static T ParseTypedOperation<T>(string dslText)
        where T : EditOperation, new()
    {
        T operation = new();
        operation.Parse(dslText);
        return operation;
    }

    /// <summary>
    /// Checks if the given text is uniquely added by only one operation in the list.
    /// </summary>
    /// <param name="ops">The list of operations to check.</param>
    /// <param name="text">The text to search for.</param>
    /// <param name="excludeIndex">The index to exclude from the search.</param>
    /// <param name="insertOnly">If true, only consider insert operations.</param>
    /// <returns>True if the text is added by only one operation, false otherwise.
    /// </returns>
    private static bool IsUniqueAddedText(List<EditOperation> ops, string text,
        int excludeIndex, bool insertOnly)
    {
        int count = 0;

        for (int i = 0; i < ops.Count; i++)
        {
            if (i == excludeIndex) continue;

            EditOperation op = ops[i];
            string? addedText = null;

            if (op is InsertBeforeEditOperation insertBefore)
            {
                addedText = insertBefore.Text;
            }
            else if (op is InsertAfterEditOperation insertAfter)
            {
                addedText = insertAfter.Text;
            }
            else if (!insertOnly && op is ReplaceEditOperation replaceOp)
            {
                addedText = replaceOp.ReplacementText;
            }

            if (addedText == text)
            {
                count++;
                if (count > 0) return false; // more than one operation adds this text
            }
        }

        return true;
    }

    /// <summary>
    /// Gets the target position for a move operation based on the add operation.
    /// Converts the add operation's position to original string coordinates.
    /// </summary>
    /// <param name="addOp">The add operation (insert or replace).</param>
    /// <param name="deleteOp">The delete operation being merged.</param>
    /// <returns>The target position for the move operation in original string
    /// coordinates.</returns>
    private static int GetTargetPositionForMove(EditOperation addOp,
        DeleteEditOperation deleteOp)
    {
        int targetPos = addOp.At;
        
        // adjust for the delete operation that would have been applied before
        // this add operation. If the add operation's position is after the
        // delete position, we need to account for the deleted characters when
        // mapping back to original coordinates
        if (targetPos > deleteOp.At) targetPos += deleteOp.Run;
        
        return addOp switch
        {
            InsertBeforeEditOperation => targetPos,
            InsertAfterEditOperation => targetPos + 1,
            ReplaceEditOperation => targetPos,
            _ => targetPos
        };
    }

    /// <summary>
    /// Adjusts the given list of edit operations by merging compatible operations
    /// into move operations where possible.
    /// </summary>
    /// <param name="ops">The list of edit operations to adjust.</param>
    /// <param name="insertOnly">If true, only consider insert operations for
    /// merging.
    /// If false, consider both insert and replace operations.</param>
    /// <returns>A new list of adjusted edit operations.</returns>
    private static IList<EditOperation> AdjustDiffOperations(
        List<EditOperation> ops, bool insertOnly = false)
    {
        if (ops.Count < 2) return [.. ops];

        List<EditOperation> adjusted = [];
        HashSet<int> processed = [];

        for (int i = 0; i < ops.Count; i++)
        {
            if (processed.Contains(i))
                continue;

            EditOperation currentOp = ops[i];

            // look for delete operations
            if (currentOp is DeleteEditOperation deleteOp &&
                !string.IsNullOrEmpty(deleteOp.InputText))
            {
                string deletedText = deleteOp.InputText;
                int matchingOpIndex = -1;

                // find a matching insert or replace operation
                for (int j = 0; j < ops.Count; j++)
                {
                    if (j == i || processed.Contains(j))
                        continue;

                    EditOperation otherOp = ops[j];
                    string? addedText = null;

                    // check insert operations
                    if (otherOp is InsertBeforeEditOperation insertBefore)
                    {
                        addedText = insertBefore.Text;
                    }
                    else if (otherOp is InsertAfterEditOperation insertAfter)
                    {
                        addedText = insertAfter.Text;
                    }
                    // check replace operations (if not insert-only)
                    else if (!insertOnly && otherOp
                        is ReplaceEditOperation replaceOp)
                    {
                        addedText = replaceOp.ReplacementText;
                    }

                    // check if the deleted text matches the added text
                    if (addedText == deletedText)
                    {
                        // verify this is the only operation adding this text
                        if (IsUniqueAddedText(ops, deletedText, j, insertOnly))
                        {
                            matchingOpIndex = j;
                            break;
                        }
                    }
                }

                if (matchingOpIndex >= 0)
                {
                    // create a move operation instead
                    EditOperation addOp = ops[matchingOpIndex];

                    MoveBeforeEditOperation moveOp = new()
                    {
                        At = deleteOp.At,
                        Run = deleteOp.Run,
                        To = GetTargetPositionForMove(addOp, deleteOp),
                        InputText = deleteOp.InputText,
                        Note = deleteOp.Note,
                        Tags = [.. deleteOp.Tags]
                    };

                    // copy notes and tags from the add operation if
                    // the delete operation doesn't have them
                    if (string.IsNullOrEmpty(moveOp.Note) &&
                        !string.IsNullOrEmpty(addOp.Note))
                    {
                        moveOp.Note = addOp.Note;
                    }

                    if (moveOp.Tags.Count == 0 && addOp.Tags.Count > 0)
                        moveOp.Tags = [.. addOp.Tags];

                    adjusted.Add(moveOp);
                    processed.Add(i);
                    processed.Add(matchingOpIndex);
                }
                else
                {
                    adjusted.Add(currentOp);
                    processed.Add(i);
                }
            }
            else
            {
                adjusted.Add(currentOp);
                processed.Add(i);
            }
        }

        return adjusted;
    }

    /// <summary>
    /// Computes the sequence of edit operations required to transform the
    /// <paramref name="source"/> string into the <paramref name="target"/> string.
    /// </summary>
    /// <remarks>The method generates a minimal sequence of edit operations,
    /// which may include insertions, deletions, replacements, or a combination
    /// of these. The operations are ordered sequentially based on their
    /// positions in the <paramref name="source"/> string.
    /// <para>If <paramref name="source"/> is empty, the method generates a
    /// single insertion operation for the entire <paramref name="target"/>
    /// string. Similarly, if <paramref name="target"/> is empty, the method
    /// generates a single deletion operation for the entire <paramref
    /// name="source"/> string.</para>
    /// <para> The method performs a character-by-character comparison and
    /// attempts to minimize the number of operations by looking ahead for
    /// matches. This ensures that the resulting sequence is efficient and
    /// concise.</para>
    /// </remarks>
    /// <param name="source">The original string to be transformed.
    /// Can be empty but not null.</param>
    /// <param name="target">The desired string after transformation. Can be
    /// empty but not null.</param>
    /// <param name="includeInputText">A boolean value indicating whether
    /// the original text involved in each edit operation should be included
    /// in the resulting operations. If <see langword="true"/>, the
    /// <c>InputText</c> property of each operation will be populated;
    /// otherwise, it will be <see langword="null"/>.</param>
    /// <param name="adjust">A boolean value indicating whether to adjust
    /// the operations by merging compatible delete and insert/replace operations
    /// into move operations. Default is true.</param>
    /// <param name="insertOnly">When <paramref name="adjust"/> is true,
    /// this parameter determines whether to only consider insert operations
    /// for merging (true) or to include replace operations as well (false).
    /// Default is false.</param>
    /// <returns>A list of <see cref="EditOperation"/> objects representing
    /// the sequence of transformations needed to convert the
    /// <paramref name="source"/> string into the <paramref name="target"/>
    /// string. The list will be empty if both strings are empty.</returns>
    public static IList<EditOperation> Diff(string source, string target,
        bool includeInputText = true, bool adjust = true, bool insertOnly = false)
    {
        List<EditOperation> operations = [];

        if (string.IsNullOrEmpty(source) && string.IsNullOrEmpty(target))
            return operations;

        if (string.IsNullOrEmpty(source))
        {
            // insert all characters from target
            InsertAfterEditOperation insert = new()
            {
                At = 0,
                Text = target,
                InputText = includeInputText ? "" : null
            };
            operations.Add(insert);
            return operations;
        }

        if (string.IsNullOrEmpty(target))
        {
            // Delete entire source
            DeleteEditOperation delete = new()
            {
                At = 1,
                Run = source.Length,
                InputText = includeInputText ? source : null
            };
            operations.Add(delete);
            return operations;
        }

        // simple character-by-character comparison
        int sourceIndex = 0;
        int targetIndex = 0;
        int currentPosition = 1;

        while (sourceIndex < source.Length && targetIndex < target.Length)
        {
            if (source[sourceIndex] == target[targetIndex])
            {
                sourceIndex++;
                targetIndex++;
                currentPosition++;
            }
            else
            {
                // look ahead to see if we can find a match
                int nextMatchInSource = FindNextMatch(source, sourceIndex,
                    target[targetIndex]);
                int nextMatchInTarget = FindNextMatch(target, targetIndex,
                    source[sourceIndex]);

                if (nextMatchInSource >= 0 &&
                    (nextMatchInTarget < 0 || nextMatchInSource <= nextMatchInTarget))
                {
                    // delete characters from source
                    string deletedText = source[sourceIndex..nextMatchInSource];
                    DeleteEditOperation delete = new()
                    {
                        At = currentPosition,
                        Run = deletedText.Length,
                        InputText = includeInputText ? deletedText : null
                    };
                    operations.Add(delete);
                    sourceIndex = nextMatchInSource;
                }
                else if (nextMatchInTarget >= 0)
                {
                    // insert characters into target
                    string insertedText = target[targetIndex..nextMatchInTarget];
                    InsertBeforeEditOperation insert = new()
                    {
                        At = currentPosition,
                        Text = insertedText,
                        InputText = includeInputText ? "" : null
                    };
                    operations.Add(insert);
                    targetIndex = nextMatchInTarget;
                    currentPosition += insertedText.Length;
                }
                else
                {
                    // replace single character
                    ReplaceEditOperation replace = new()
                    {
                        At = currentPosition,
                        Run = 1,
                        ReplacementText = target[targetIndex].ToString(),
                        InputText = includeInputText
                            ? source[sourceIndex].ToString() : null
                    };
                    operations.Add(replace);
                    sourceIndex++;
                    targetIndex++;
                    currentPosition++;
                }
            }
        }

        // handle remaining characters
        if (sourceIndex < source.Length)
        {
            string remainingSource = source[sourceIndex..];
            DeleteEditOperation delete = new()
            {
                At = currentPosition,
                Run = remainingSource.Length,
                InputText = includeInputText ? remainingSource : null
            };
            operations.Add(delete);
        }

        if (targetIndex < target.Length)
        {
            string remainingTarget = target[targetIndex..];
            InsertAfterEditOperation insert = new()
            {
                At = currentPosition - 1,
                Text = remainingTarget,
                InputText = includeInputText ? "" : null
            };
            operations.Add(insert);
        }

        // apply adjustments if requested
        if (adjust)
        {
            operations = (List<EditOperation>)
                AdjustDiffOperations(operations, insertOnly);
        }

        return operations;
    }

    private static int FindNextMatch(string text, int startIndex, char target)
    {
        for (int i = startIndex; i < text.Length; i++)
        {
            if (text[i] == target)
                return i;
        }
        return -1;
    }
}
