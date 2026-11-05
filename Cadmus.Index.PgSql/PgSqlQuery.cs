using Npgsql;
using NpgsqlTypes;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cadmus.Index.PgSql;

/// <summary>
/// A parameter of a <see cref="PgSqlQuery"/>. Parameters are positional,
/// i.e. the first parameter is <c>$1</c>, the second <c>$2</c>, etc.
/// </summary>
/// <param name="Value">The value.</param>
/// <param name="Type">The type.</param>
public sealed record PgSqlQueryParameter(object Value, NpgsqlDbType Type)
{
    /// <summary>
    /// Creates a new positional Npgsql parameter from this parameter.
    /// </summary>
    /// <returns>Parameter.</returns>
    public NpgsqlParameter ToNpgsqlParameter() =>
        new() { Value = Value, NpgsqlDbType = Type };

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString() =>
        $"{Convert.ToString(Value, CultureInfo.InvariantCulture)} ({Type})";
}

/// <summary>
/// A parameterized PostgreSQL index query, built by
/// <see cref="PgSqlQueryBuilder"/>. This includes 2 statements: one for
/// counting all the matches, and another for getting a page of them.
/// </summary>
public sealed class PgSqlQuery
{
    /// <summary>
    /// Gets the SQL code counting all the matches.
    /// </summary>
    public string CountSql { get; }

    /// <summary>
    /// Gets the SQL code getting a page of matches.
    /// </summary>
    public string PageSql { get; }

    /// <summary>
    /// Gets the parameters of <see cref="CountSql"/>.
    /// </summary>
    public IReadOnlyList<PgSqlQueryParameter> CountParameters { get; }

    /// <summary>
    /// Gets the parameters of <see cref="PageSql"/>. These are equal to
    /// <see cref="CountParameters"/> plus the eventual paging parameters
    /// (limit and offset).
    /// </summary>
    public IReadOnlyList<PgSqlQueryParameter> PageParameters { get; }

    /// <summary>
    /// Gets the minimum similarity threshold among those of the similarity
    /// clauses in the query, or null if the query has no such clauses.
    /// When not null, <c>pg_trgm.similarity_threshold</c> must be set to
    /// this value before executing the query, so that the trigram indexes
    /// can be used for similarity.
    /// </summary>
    public double? SimilarityThreshold { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PgSqlQuery"/> class.
    /// </summary>
    /// <param name="countSql">The count SQL.</param>
    /// <param name="pageSql">The page SQL.</param>
    /// <param name="countParameters">The count parameters.</param>
    /// <param name="pageParameters">The page parameters.</param>
    /// <param name="similarityThreshold">The similarity threshold.</param>
    /// <exception cref="ArgumentNullException">any argument</exception>
    public PgSqlQuery(string countSql, string pageSql,
        IReadOnlyList<PgSqlQueryParameter> countParameters,
        IReadOnlyList<PgSqlQueryParameter> pageParameters,
        double? similarityThreshold)
    {
        CountSql = countSql ?? throw new ArgumentNullException(nameof(countSql));
        PageSql = pageSql ?? throw new ArgumentNullException(nameof(pageSql));
        CountParameters = countParameters ??
            throw new ArgumentNullException(nameof(countParameters));
        PageParameters = pageParameters ??
            throw new ArgumentNullException(nameof(pageParameters));
        SimilarityThreshold = similarityThreshold;
    }

    /// <summary>
    /// Converts to string, for diagnostic purposes.
    /// </summary>
    /// <returns>String with SQL code and parameters.</returns>
    public override string ToString()
    {
        StringBuilder sb = new();
        if (SimilarityThreshold.HasValue)
        {
            sb.Append("-- pg_trgm.similarity_threshold = ")
              .AppendLine(SimilarityThreshold.Value.ToString(
                  CultureInfo.InvariantCulture));
        }
        sb.AppendLine(CountSql + ";").AppendLine(PageSql + ";");
        sb.Append(string.Join(Environment.NewLine,
            PageParameters.Select((p, i) => $"-- ${i + 1} = {p}")));
        return sb.ToString();
    }
}
