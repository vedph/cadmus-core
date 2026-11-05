using Cadmus.Core.Config;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Index.Search;

/// <summary>
/// Index query parser. This parses the text of an index query into an
/// abstract syntax tree (<see cref="IndexQueryNode"/>), validating it.
/// </summary>
/// <remarks>
/// <para>Syntax:</para>
/// <code>
/// query   := or
/// or      := and ("OR" and)*
/// and     := unary (["AND"] unary)*
/// unary   := "NOT" unary | primary
/// primary := clause | "(" or ")"
/// clause  := "[" name operator value "]"
/// </code>
/// <para>Keywords are case-insensitive; two consecutive operands without
/// a keyword between them are connected with AND. A query without any
/// <c>[</c> is a shortcut for <c>[title*=query]</c>.</para>
/// <para>The name is either a field alias (case-insensitive), or the name
/// of a pin. A name prefixed with <c>@</c> is always the name of a pin,
/// even if it is equal to a field alias (e.g. <c>[@title=x]</c> targets
/// pins named <c>title</c>).</para>
/// <para>Value escapes: <c>\[</c>, <c>\]</c>, <c>\\</c> for the literal
/// characters, and <c>\HHHH</c> for any UTF-16 code expressed in 4 hex
/// digits. Any other backslash is kept as such.</para>
/// </remarks>
public sealed partial class IndexQueryParser
{
    /// <summary>
    /// The maximum number of clauses in a query.
    /// </summary>
    public const int MAX_CLAUSES = 64;

    /// <summary>
    /// The maximum nesting depth of brackets in a query.
    /// </summary>
    public const int MAX_DEPTH = 16;

    /// <summary>
    /// The default similarity threshold for the similarity operator.
    /// </summary>
    public const double DEFAULT_SIMILARITY_THRESHOLD = 0.9;

    // operators sorted so that the longest ones come first
    private static readonly (string Text, IndexQueryOperator Op)[] _operators =
    [
        ("==", IndexQueryOperator.NumEqual),
        ("<>", IndexQueryOperator.NotEqual),
        ("*=", IndexQueryOperator.Contains),
        ("^=", IndexQueryOperator.StartsWith),
        ("$=", IndexQueryOperator.EndsWith),
        ("?=", IndexQueryOperator.Wildcards),
        ("~=", IndexQueryOperator.Regex),
        ("%=", IndexQueryOperator.Similar),
        ("!=", IndexQueryOperator.NumNotEqual),
        ("<=", IndexQueryOperator.NumLessOrEqual),
        (">=", IndexQueryOperator.NumGreaterOrEqual),
        ("&:", IndexQueryOperator.HasAllFlags),
        ("!:", IndexQueryOperator.HasNoFlags),
        ("=", IndexQueryOperator.Equal),
        ("<", IndexQueryOperator.NumLess),
        (">", IndexQueryOperator.NumGreater),
        (":", IndexQueryOperator.HasAnyFlags),
    ];

    private const string OPERATOR_CHARS = "=<>*^$?~%!:&";

    private static readonly Dictionary<string, IndexQueryField> _fields =
        new(StringComparer.OrdinalIgnoreCase)
    {
        ["itemid"] = IndexQueryField.ItemId,
        ["title"] = IndexQueryField.Title,
        ["t"] = IndexQueryField.Title,
        ["description"] = IndexQueryField.Description,
        ["dsc"] = IndexQueryField.Description,
        ["facetid"] = IndexQueryField.FacetId,
        ["facet"] = IndexQueryField.FacetId,
        ["groupid"] = IndexQueryField.GroupId,
        ["group"] = IndexQueryField.GroupId,
        ["sortkey"] = IndexQueryField.SortKey,
        ["flags"] = IndexQueryField.Flags,
        ["creatorid"] = IndexQueryField.CreatorId,
        ["userid"] = IndexQueryField.UserId,
        ["timecreated"] = IndexQueryField.TimeCreated,
        ["timemodified"] = IndexQueryField.TimeModified,
        ["partid"] = IndexQueryField.PartId,
        ["parttypeid"] = IndexQueryField.PartTypeId,
        ["parttype"] = IndexQueryField.PartTypeId,
        ["type"] = IndexQueryField.PartTypeId,
        ["roleid"] = IndexQueryField.RoleId,
        ["role"] = IndexQueryField.RoleId,
        ["name"] = IndexQueryField.Name,
        ["n"] = IndexQueryField.Name,
        ["value"] = IndexQueryField.Value,
        ["v"] = IndexQueryField.Value,
        ["timeindexed"] = IndexQueryField.TimeIndexed,
        ["time"] = IndexQueryField.TimeIndexed,
    };

    private readonly Dictionary<string, int> _flags;

    [GeneratedRegex(@":(\d+(?:\.\d+)?)$")]
    private static partial Regex ThresholdRegex();

    [GeneratedRegex("^[0-9a-fA-F]{1,8}$")]
    private static partial Regex HexRegex();

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexQueryParser"/>
    /// class.
    /// </summary>
    /// <param name="flags">The optional flag definitions, used to resolve
    /// flag labels in flags clauses.</param>
    public IndexQueryParser(IEnumerable<FlagDefinition>? flags = null)
    {
        _flags = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (flags != null)
        {
            foreach (FlagDefinition def in flags)
            {
                if (!string.IsNullOrWhiteSpace(def.Label))
                    _flags[def.Label.Trim()] = def.Id;
            }
        }
    }

    #region Tokenizer
    private enum TokenType
    {
        End,
        Clause,
        LeftBracket,
        RightBracket,
        And,
        Or,
        Not
    }

    private readonly record struct Token(TokenType Type, int Position,
        string? Text = null);

    private static List<Token> Tokenize(string query)
    {
        List<Token> tokens = [];
        int i = 0;
        while (i < query.Length)
        {
            char c = query[i];
            if (char.IsWhiteSpace(c))
            {
                i++;
                continue;
            }
            switch (c)
            {
                case '[':
                    int start = i++;
                    StringBuilder sb = new();
                    bool closed = false;
                    while (i < query.Length)
                    {
                        if (query[i] == '\\' && i + 1 < query.Length)
                        {
                            // keep escapes: they are resolved in values
                            sb.Append(query, i, 2);
                            i += 2;
                            continue;
                        }
                        if (query[i] == ']')
                        {
                            closed = true;
                            i++;
                            break;
                        }
                        sb.Append(query[i++]);
                    }
                    if (!closed)
                    {
                        throw new CadmusQueryException(
                            $"Unterminated clause at {start}", start);
                    }
                    tokens.Add(new Token(TokenType.Clause, start,
                        sb.ToString()));
                    break;
                case '(':
                    tokens.Add(new Token(TokenType.LeftBracket, i++));
                    break;
                case ')':
                    tokens.Add(new Token(TokenType.RightBracket, i++));
                    break;
                default:
                    int wordStart = i;
                    while (i < query.Length && char.IsLetter(query[i])) i++;
                    string word = query[wordStart..i];
                    switch (word.ToUpperInvariant())
                    {
                        case "AND":
                            tokens.Add(new Token(TokenType.And, wordStart));
                            break;
                        case "OR":
                            tokens.Add(new Token(TokenType.Or, wordStart));
                            break;
                        case "NOT":
                            tokens.Add(new Token(TokenType.Not, wordStart));
                            break;
                        default:
                            string bad = word.Length > 0
                                ? word : query[wordStart].ToString();
                            throw new CadmusQueryException(
                                $"Unexpected \"{bad}\" at {wordStart}: " +
                                "expected a clause between [], a bracket, " +
                                "AND, OR, or NOT", wordStart);
                    }
                    break;
            }
        }
        tokens.Add(new Token(TokenType.End, query.Length));
        return tokens;
    }
    #endregion

    #region Values
    private static int HexValue(char c) => c switch
    {
        >= '0' and <= '9' => c - '0',
        >= 'a' and <= 'f' => c - 'a' + 10,
        >= 'A' and <= 'F' => c - 'A' + 10,
        _ => -1
    };

    /// <summary>
    /// Unescapes the specified value.
    /// </summary>
    /// <param name="value">The value.</param>
    /// <returns>Unescaped value.</returns>
    public static string Unescape(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (!value.Contains('\\')) return value;

        StringBuilder sb = new(value.Length);
        int i = 0;
        while (i < value.Length)
        {
            char c = value[i];
            if (c == '\\' && i + 1 < value.Length)
            {
                char next = value[i + 1];
                if (next is '[' or ']' or '\\')
                {
                    sb.Append(next);
                    i += 2;
                    continue;
                }
                if (i + 4 < value.Length
                    && HexValue(value[i + 1]) > -1
                    && HexValue(value[i + 2]) > -1
                    && HexValue(value[i + 3]) > -1
                    && HexValue(value[i + 4]) > -1)
                {
                    sb.Append((char)((HexValue(value[i + 1]) << 12)
                        | (HexValue(value[i + 2]) << 8)
                        | (HexValue(value[i + 3]) << 4)
                        | HexValue(value[i + 4])));
                    i += 5;
                    continue;
                }
            }
            sb.Append(c);
            i++;
        }
        return sb.ToString();
    }

    private int ParseFlags(string value, int position)
    {
        int n = 0;
        bool any = false;
        foreach (string token in value.Split(',',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            any = true;
            // labels take precedence over hex values, as some labels
            // (e.g. "bad") could be valid hex numbers
            if (_flags.TryGetValue(token, out int flag))
            {
                n |= flag;
            }
            else if (HexRegex().IsMatch(token))
            {
                n |= (int)uint.Parse(token, NumberStyles.HexNumber,
                    CultureInfo.InvariantCulture);
            }
            else
            {
                throw new CadmusQueryException(
                    $"Unknown flag \"{token}\" at {position}", position);
            }
        }
        if (!any)
        {
            throw new CadmusQueryException(
                $"No flags specified at {position}", position);
        }
        return n;
    }

    private static decimal ParseNumber(string value, int position)
    {
        if (!decimal.TryParse(value.Trim(), NumberStyles.Float,
            CultureInfo.InvariantCulture, out decimal n))
        {
            throw new CadmusQueryException(
                $"Invalid number \"{value}\" at {position}", position);
        }
        return n;
    }

    private static DateTime ParseDate(string value, int position)
    {
        if (!DateTime.TryParse(value.Trim(), CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out DateTime dt))
        {
            throw new CadmusQueryException(
                $"Invalid date \"{value}\" at {position}", position);
        }
        return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
    }
    #endregion

    #region Clause
    private static (IndexQueryField Field, string? PinName) ParseName(
        string name, int position)
    {
        if (name.Length == 0)
        {
            throw new CadmusQueryException(
                $"Missing name in clause at {position}", position);
        }
        if (name[0] == '@')
        {
            string pinName = name[1..].Trim();
            if (pinName.Length == 0)
            {
                throw new CadmusQueryException(
                    $"Missing pin name after @ in clause at {position}",
                    position);
            }
            return (IndexQueryField.Pin, pinName);
        }
        return _fields.TryGetValue(name, out IndexQueryField field)
            ? (field, null)
            : (IndexQueryField.Pin, name);
    }

    private static IndexQueryOperator ToNumericOperator(IndexQueryOperator op)
        => op switch
        {
            IndexQueryOperator.Equal => IndexQueryOperator.NumEqual,
            IndexQueryOperator.NotEqual => IndexQueryOperator.NumNotEqual,
            _ => op
        };

    private static bool IsNumeric(IndexQueryOperator op) =>
        op is >= IndexQueryOperator.NumEqual
            and <= IndexQueryOperator.NumGreaterOrEqual;

    private static bool IsFlagsOperator(IndexQueryOperator op) =>
        op is >= IndexQueryOperator.HasAnyFlags;

    private IndexQueryClause ParseClause(string text, int position)
    {
        // name: up to the first operator character
        int i = 0;
        while (i < text.Length && !OPERATOR_CHARS.Contains(text[i])) i++;
        if (i == text.Length)
        {
            throw new CadmusQueryException(
                $"Missing operator in clause at {position}", position);
        }

        string name = text[..i].Trim();
        int opIndex = Array.FindIndex(_operators,
            o => string.CompareOrdinal(text, i, o.Text, 0, o.Text.Length) == 0);
        if (opIndex == -1)
        {
            throw new CadmusQueryException(
                $"Invalid operator in clause at {position}", position);
        }
        IndexQueryOperator op = _operators[opIndex].Op;
        string value = Unescape(
            text[(i + _operators[opIndex].Text.Length)..].Trim());

        (IndexQueryField field, string? pinName) = ParseName(name, position);

        // time fields: comparisons with dates
        if (field.IsTimeField())
        {
            op = ToNumericOperator(op);
            if (!IsNumeric(op))
            {
                throw new CadmusQueryException(
                    $"Invalid operator for time field \"{name}\" " +
                    $"at {position}: use =, <>, ==, !=, <, <=, >, >=",
                    position);
            }
            return new IndexQueryClause
            {
                Field = field,
                Operator = op,
                Value = value,
                Date = ParseDate(value, position),
                Position = position
            };
        }

        // flags field: flags operators, equality to a mask, or numbers
        if (field == IndexQueryField.Flags)
        {
            if (IsFlagsOperator(op)
                || op is IndexQueryOperator.Equal or IndexQueryOperator.NotEqual)
            {
                return new IndexQueryClause
                {
                    Field = field,
                    Operator = op,
                    Value = value,
                    Flags = ParseFlags(value, position),
                    Position = position
                };
            }
            if (!IsNumeric(op))
            {
                throw new CadmusQueryException(
                    $"Invalid operator for flags at {position}", position);
            }
            decimal n = ParseNumber(value, position);
            return new IndexQueryClause
            {
                Field = field,
                Operator = op,
                Value = value,
                Number = n,
                Position = position
            };
        }

        if (IsFlagsOperator(op))
        {
            throw new CadmusQueryException(
                $"Flags operators can be used only with flags, at {position}",
                position);
        }

        switch (op)
        {
            case IndexQueryOperator.Wildcards:
                // without wildcards, this is just an equality
                if (value.IndexOfAny(['*', '?']) == -1)
                    op = IndexQueryOperator.Equal;
                break;

            case IndexQueryOperator.Regex:
                if (value.Length == 0)
                {
                    throw new CadmusQueryException(
                        $"Empty regular expression at {position}", position);
                }
                break;

            case IndexQueryOperator.Similar:
                double threshold = DEFAULT_SIMILARITY_THRESHOLD;
                Match m = ThresholdRegex().Match(value);
                if (m.Success)
                {
                    threshold = double.Parse(m.Groups[1].Value,
                        CultureInfo.InvariantCulture);
                    if (threshold <= 0 || threshold > 1)
                    {
                        throw new CadmusQueryException(
                            "Similarity threshold must be greater than 0 " +
                            $"and not greater than 1, at {position}", position);
                    }
                    value = value[..m.Index];
                }
                if (value.Length == 0)
                {
                    throw new CadmusQueryException(
                        $"Empty similarity value at {position}", position);
                }
                return new IndexQueryClause
                {
                    Field = field,
                    PinName = pinName,
                    Operator = op,
                    Value = value,
                    Threshold = threshold,
                    Position = position
                };

            default:
                if (IsNumeric(op))
                {
                    return new IndexQueryClause
                    {
                        Field = field,
                        PinName = pinName,
                        Operator = op,
                        Value = value,
                        Number = ParseNumber(value, position),
                        Position = position
                    };
                }
                break;
        }

        return new IndexQueryClause
        {
            Field = field,
            PinName = pinName,
            Operator = op,
            Value = value,
            Position = position
        };
    }
    #endregion

    #region Parser
    private sealed class ParserState(List<Token> tokens)
    {
        private int _index;
        public int ClauseCount { get; set; }
        public Token Current => tokens[_index];
        public void Next() { if (_index < tokens.Count - 1) _index++; }
    }

    private IndexQueryNode ParseOr(ParserState state, int depth)
    {
        List<IndexQueryNode> nodes = [ParseAnd(state, depth)];
        while (state.Current.Type == TokenType.Or)
        {
            state.Next();
            nodes.Add(ParseAnd(state, depth));
        }
        return nodes.Count == 1
            ? nodes[0]
            : new IndexQueryLogicalNode(IndexQueryLogicalOperator.Or, nodes);
    }

    private IndexQueryNode ParseAnd(ParserState state, int depth)
    {
        List<IndexQueryNode> nodes = [ParseUnary(state, depth)];
        while (true)
        {
            TokenType type = state.Current.Type;
            if (type == TokenType.And)
            {
                state.Next();
                nodes.Add(ParseUnary(state, depth));
            }
            else if (type is TokenType.Clause or TokenType.LeftBracket
                or TokenType.Not)
            {
                // implicit AND
                nodes.Add(ParseUnary(state, depth));
            }
            else break;
        }
        return nodes.Count == 1
            ? nodes[0]
            : new IndexQueryLogicalNode(IndexQueryLogicalOperator.And, nodes);
    }

    private IndexQueryNode ParseUnary(ParserState state, int depth)
    {
        if (state.Current.Type == TokenType.Not)
        {
            state.Next();
            return new IndexQueryNotNode(ParseUnary(state, depth));
        }
        return ParsePrimary(state, depth);
    }

    private IndexQueryNode ParsePrimary(ParserState state, int depth)
    {
        Token token = state.Current;
        switch (token.Type)
        {
            case TokenType.Clause:
                if (++state.ClauseCount > MAX_CLAUSES)
                {
                    throw new CadmusQueryException(
                        $"Too many clauses (max {MAX_CLAUSES})", token.Position);
                }
                state.Next();
                return ParseClause(token.Text!, token.Position);

            case TokenType.LeftBracket:
                if (depth + 1 > MAX_DEPTH)
                {
                    throw new CadmusQueryException(
                        $"Too many nested brackets (max {MAX_DEPTH})",
                        token.Position);
                }
                state.Next();
                IndexQueryNode node = ParseOr(state, depth + 1);
                if (state.Current.Type != TokenType.RightBracket)
                {
                    throw new CadmusQueryException(
                        $"Missing ) for ( at {token.Position}",
                        state.Current.Position);
                }
                state.Next();
                return node;

            default:
                throw new CadmusQueryException(
                    token.Type == TokenType.End
                        ? "Unexpected end of query: expected a clause or ("
                        : $"Unexpected {token.Type} at {token.Position}: " +
                          "expected a clause or (",
                    token.Position);
        }
    }

    /// <summary>
    /// Parses the specified query.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <returns>The root node of the query.</returns>
    /// <exception cref="ArgumentNullException">query</exception>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    public IndexQueryNode Parse(string query)
    {
        ArgumentNullException.ThrowIfNull(query);

        string trimmed = query.Trim();
        if (trimmed.Length == 0)
            throw new CadmusQueryException("Empty query", 0);

        // shortcut: plain text is searched in titles
        if (!trimmed.Contains('['))
        {
            return new IndexQueryClause
            {
                Field = IndexQueryField.Title,
                Operator = IndexQueryOperator.Contains,
                Value = trimmed,
                Position = 0
            };
        }

        ParserState state = new(Tokenize(query));
        IndexQueryNode root = ParseOr(state, 0);
        if (state.Current.Type != TokenType.End)
        {
            throw new CadmusQueryException(
                $"Unexpected {state.Current.Type} at {state.Current.Position}",
                state.Current.Position);
        }
        return root;
    }
    #endregion
}
