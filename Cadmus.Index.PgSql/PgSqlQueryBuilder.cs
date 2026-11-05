using Cadmus.Index.Search;
using Fusi.Tools.Data;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cadmus.Index.PgSql;

/// <summary>
/// PostgreSQL index query builder. This builds parameterized SQL code from
/// a parsed index query. All the values are passed as parameters, so that
/// no user input ever becomes part of the SQL code.
/// </summary>
/// <remarks>
/// <para>In item queries, each clause on pins is an <c>EXISTS</c> subquery,
/// so that each clause can be matched by any pin of the item (whatever the
/// part providing it), and items without pins can still be matched by
/// clauses on their own fields.</para>
/// <para>In pin queries, each clause is matched against the same pin
/// (joined to its item when the query refers to item fields).</para>
/// </remarks>
public sealed class PgSqlQueryBuilder
{
    private const string ITEM_COLUMNS =
        "i.id, i.title, i.description, i.facet_id, i.group_id, i.sort_key, " +
        "i.flags, i.time_created, i.creator_id, i.time_modified, i.user_id";

    private const string PIN_COLUMNS =
        "p.item_id, p.part_id, p.part_type_id, p.part_role_id, p.name, p.value";

    // a decimal number, as accepted by the numeric type
    private const string NUMBER_PATTERN =
        "'^[[:space:]]*[-+]?([0-9]+([.][0-9]*)?|[.][0-9]+)[[:space:]]*$'";

    private sealed class BuildContext(bool itemMode)
    {
        public bool ItemMode { get; } = itemMode;
        public List<PgSqlQueryParameter> Parameters { get; } = [];
        public bool NeedsItemJoin { get; set; }
        public double? MinThreshold { get; set; }

        public string Add(object value, NpgsqlDbType type)
        {
            Parameters.Add(new PgSqlQueryParameter(value, type));
            return "$" + Parameters.Count;
        }
    }

    /// <summary>
    /// Escapes the LIKE wildcards in the specified text (using the default
    /// LIKE escape character, the backslash).
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>Escaped text.</returns>
    public static string EscapeLike(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return text.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
    }

    /// <summary>
    /// Converts the specified text with wildcards (<c>?</c>=any single
    /// character, <c>*</c>=0 or more characters) into a LIKE pattern.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>LIKE pattern.</returns>
    public static string WildcardsToLike(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        StringBuilder sb = new(text.Length + 4);
        foreach (char c in text)
        {
            switch (c)
            {
                case '*': sb.Append('%'); break;
                case '?': sb.Append('_'); break;
                case '%': sb.Append("\\%"); break;
                case '_': sb.Append("\\_"); break;
                case '\\': sb.Append("\\\\"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    private static string GetColumn(IndexQueryField field, BuildContext context)
    {
        string? column = field switch
        {
            IndexQueryField.ItemId => context.ItemMode ? "i.id" : "p.item_id",
            IndexQueryField.Pin or IndexQueryField.Value => "p.value",
            IndexQueryField.PartId => "p.part_id",
            IndexQueryField.PartTypeId => "p.part_type_id",
            IndexQueryField.RoleId => "p.part_role_id",
            IndexQueryField.Name => "p.name",
            IndexQueryField.TimeIndexed => "p.time_indexed",
            _ => null
        };
        if (column != null) return column;

        // item fields: in pin queries, these require joining the item
        if (!context.ItemMode) context.NeedsItemJoin = true;
        return field switch
        {
            IndexQueryField.Title => "i.title",
            IndexQueryField.Description => "i.description",
            IndexQueryField.FacetId => "i.facet_id",
            IndexQueryField.GroupId => "i.group_id",
            IndexQueryField.SortKey => "i.sort_key",
            IndexQueryField.Flags => "i.flags",
            IndexQueryField.CreatorId => "i.creator_id",
            IndexQueryField.UserId => "i.user_id",
            IndexQueryField.TimeCreated => "i.time_created",
            IndexQueryField.TimeModified => "i.time_modified",
            _ => throw new ArgumentOutOfRangeException(nameof(field), field,
                "Unsupported field")
        };
    }

    private static string GetComparison(IndexQueryOperator op) => op switch
    {
        IndexQueryOperator.NumEqual => "=",
        IndexQueryOperator.NumNotEqual => "<>",
        IndexQueryOperator.NumLess => "<",
        IndexQueryOperator.NumLessOrEqual => "<=",
        IndexQueryOperator.NumGreater => ">",
        IndexQueryOperator.NumGreaterOrEqual => ">=",
        _ => throw new ArgumentOutOfRangeException(nameof(op), op,
            "Not a comparison operator")
    };

    private static string BuildCondition(IndexQueryClause clause,
        BuildContext context)
    {
        string col = GetColumn(clause.Field, context);
        bool nullable = clause.Field.IsNullable();

        switch (clause.Operator)
        {
            case IndexQueryOperator.Equal:
                if (clause.Flags.HasValue)
                    return $"{col} = {context.Add(clause.Flags.Value, NpgsqlDbType.Integer)}";
                if (nullable && clause.Value.Length == 0)
                    return $"({col} IS NULL OR {col} = '')";
                return $"{col} = {context.Add(clause.Value, NpgsqlDbType.Text)}";

            case IndexQueryOperator.NotEqual:
                if (clause.Flags.HasValue)
                    return $"{col} <> {context.Add(clause.Flags.Value, NpgsqlDbType.Integer)}";
                if (nullable)
                {
                    return clause.Value.Length == 0
                        ? $"({col} IS NOT NULL AND {col} <> '')"
                        : $"{col} IS DISTINCT FROM {context.Add(clause.Value, NpgsqlDbType.Text)}";
                }
                return $"{col} <> {context.Add(clause.Value, NpgsqlDbType.Text)}";

            case IndexQueryOperator.Contains:
                return $"{col} LIKE {context.Add(
                    "%" + EscapeLike(clause.Value) + "%", NpgsqlDbType.Text)}";

            case IndexQueryOperator.StartsWith:
                return $"{col} LIKE {context.Add(
                    EscapeLike(clause.Value) + "%", NpgsqlDbType.Text)}";

            case IndexQueryOperator.EndsWith:
                return $"{col} LIKE {context.Add(
                    "%" + EscapeLike(clause.Value), NpgsqlDbType.Text)}";

            case IndexQueryOperator.Wildcards:
                return $"{col} LIKE {context.Add(
                    WildcardsToLike(clause.Value), NpgsqlDbType.Text)}";

            case IndexQueryOperator.Regex:
                return $"{col} ~ {context.Add(clause.Value, NpgsqlDbType.Text)}";

            case IndexQueryOperator.Similar:
                double threshold = clause.Threshold
                    ?? IndexQueryParser.DEFAULT_SIMILARITY_THRESHOLD;
                if (context.MinThreshold == null || threshold < context.MinThreshold)
                    context.MinThreshold = threshold;
                string value = context.Add(clause.Value, NpgsqlDbType.Text);
                // % uses the trigram indexes with the threshold set in
                // pg_trgm.similarity_threshold (the minimum of all the
                // thresholds); similarity() applies the clause threshold
                return $"{col} % {value} AND similarity({col}, {value}) >= " +
                    context.Add(threshold, NpgsqlDbType.Double);

            case IndexQueryOperator.HasAnyFlags:
                return $"({col} & {context.Add(clause.Flags!.Value, NpgsqlDbType.Integer)}) <> 0";

            case IndexQueryOperator.HasAllFlags:
                string mask = context.Add(clause.Flags!.Value, NpgsqlDbType.Integer);
                return $"({col} & {mask}) = {mask}";

            case IndexQueryOperator.HasNoFlags:
                return $"({col} & {context.Add(clause.Flags!.Value, NpgsqlDbType.Integer)}) = 0";

            default:
                string cmp = GetComparison(clause.Operator);
                if (clause.Date.HasValue)
                {
                    return $"{col} {cmp} " +
                        context.Add(clause.Date.Value, NpgsqlDbType.TimestampTz);
                }
                string n = context.Add(clause.Number!.Value, NpgsqlDbType.Numeric);
                if (clause.Field == IndexQueryField.Flags)
                    return $"{col} {cmp} {n}";
                // non-numeric values are excluded (NULL never matches)
                return $"(CASE WHEN {col} ~ {NUMBER_PATTERN} " +
                    $"THEN {col}::numeric END) {cmp} {n}";
        }
    }

    private static string BuildClause(IndexQueryClause clause,
        BuildContext context)
    {
        string condition = BuildCondition(clause, context);
        if (clause.Field == IndexQueryField.Pin)
        {
            condition = "p.name = " +
                context.Add(clause.PinName!, NpgsqlDbType.Text) +
                " AND " + condition;
        }

        return context.ItemMode && clause.Field.IsPinField()
            ? "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND " +
              condition + ")"
            : "(" + condition + ")";
    }

    private static string BuildWhere(IndexQueryNode node, BuildContext context)
    {
        return node switch
        {
            IndexQueryClause clause => BuildClause(clause, context),
            IndexQueryNotNode not => "NOT " + BuildWhere(not.Operand, context),
            IndexQueryLogicalNode logical => "(" + string.Join(
                logical.Operator == IndexQueryLogicalOperator.And
                    ? " AND " : " OR ",
                logical.Children.Select(c => BuildWhere(c, context))) + ")",
            _ => throw new ArgumentException(
                "Unsupported query node: " + node.GetType().Name, nameof(node))
        };
    }

    private static PgSqlQuery Build(BuildContext context, string from,
        string where, string columns, string orderBy, PagingOptions options)
    {
        string countSql = $"SELECT COUNT(*) FROM {from} WHERE {where}";
        StringBuilder page = new($"SELECT {columns} FROM {from} " +
            $"WHERE {where} ORDER BY {orderBy}");

        List<PgSqlQueryParameter> pageParameters = [.. context.Parameters];
        if (options.PageSize > 0)
        {
            int pageNumber = Math.Max(options.PageNumber, 1);
            pageParameters.Add(new PgSqlQueryParameter(options.PageSize,
                NpgsqlDbType.Integer));
            page.Append(" LIMIT $").Append(pageParameters.Count);
            pageParameters.Add(new PgSqlQueryParameter(
                (long)(pageNumber - 1) * options.PageSize, NpgsqlDbType.Bigint));
            page.Append(" OFFSET $").Append(pageParameters.Count);
        }

        return new PgSqlQuery(countSql, page.ToString(),
            context.Parameters, pageParameters, context.MinThreshold);
    }

    /// <summary>
    /// Builds the query for searching items. Items are sorted by their sort
    /// key, and then by their ID.
    /// </summary>
    /// <param name="query">The parsed query.</param>
    /// <param name="options">The paging options. A page size equal to 0
    /// means no paging.</param>
    /// <returns>Query.</returns>
    /// <exception cref="ArgumentNullException">query or options</exception>
    public PgSqlQuery BuildItemQuery(IndexQueryNode query, PagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        BuildContext context = new(true);
        string where = BuildWhere(query, context);
        return Build(context, "item i", where, ITEM_COLUMNS,
            "i.sort_key, i.id", options);
    }

    /// <summary>
    /// Builds the query for searching pins. Pins are sorted by name, value,
    /// and then by their ID.
    /// </summary>
    /// <param name="query">The parsed query.</param>
    /// <param name="options">The paging options. A page size equal to 0
    /// means no paging.</param>
    /// <returns>Query.</returns>
    /// <exception cref="ArgumentNullException">query or options</exception>
    public PgSqlQuery BuildPinQuery(IndexQueryNode query, PagingOptions options)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(options);

        BuildContext context = new(false);
        string where = BuildWhere(query, context);
        string from = context.NeedsItemJoin
            ? "pin p JOIN item i ON i.id = p.item_id"
            : "pin p";
        return Build(context, from, where, PIN_COLUMNS,
            "p.name, p.value, p.id", options);
    }

    /// <summary>
    /// Parses the specified query text and builds the query for searching
    /// items.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options.</param>
    /// <param name="parser">The optional parser to use.</param>
    /// <returns>Query.</returns>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    public PgSqlQuery BuildItemQuery(string query, PagingOptions options,
        IndexQueryParser? parser = null) =>
        BuildItemQuery((parser ?? new IndexQueryParser()).Parse(query), options);

    /// <summary>
    /// Parses the specified query text and builds the query for searching
    /// pins.
    /// </summary>
    /// <param name="query">The query text.</param>
    /// <param name="options">The paging options.</param>
    /// <param name="parser">The optional parser to use.</param>
    /// <returns>Query.</returns>
    /// <exception cref="CadmusQueryException">invalid query</exception>
    public PgSqlQuery BuildPinQuery(string query, PagingOptions options,
        IndexQueryParser? parser = null) =>
        BuildPinQuery((parser ?? new IndexQueryParser()).Parse(query), options);
}
