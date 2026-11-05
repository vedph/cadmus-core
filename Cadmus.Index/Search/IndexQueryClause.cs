using System;

namespace Cadmus.Index.Search;

/// <summary>
/// A clause in an index query, i.e. a field/operator/value triple.
/// Clauses are validated when parsed, and values are pre-processed
/// according to the operator: so, the typed properties
/// (<see cref="Number"/>, <see cref="Date"/>, <see cref="Flags"/>,
/// <see cref="Threshold"/>) are set when relevant.
/// </summary>
/// <seealso cref="IndexQueryNode" />
public sealed class IndexQueryClause : IndexQueryNode
{
    /// <summary>
    /// Gets or sets the target field.
    /// </summary>
    public IndexQueryField Field { get; init; }

    /// <summary>
    /// Gets or sets the pin name, when <see cref="Field"/> is
    /// <see cref="IndexQueryField.Pin"/>.
    /// </summary>
    public string? PinName { get; init; }

    /// <summary>
    /// Gets or sets the operator.
    /// </summary>
    public IndexQueryOperator Operator { get; init; }

    /// <summary>
    /// Gets or sets the value, unescaped. For <see cref="IndexQueryOperator.Similar"/>
    /// this does not include the threshold.
    /// </summary>
    public string Value { get; init; } = "";

    /// <summary>
    /// Gets or sets the numeric value for numeric operators applied to
    /// non-time fields.
    /// </summary>
    public decimal? Number { get; init; }

    /// <summary>
    /// Gets or sets the UTC date value for operators applied to time fields.
    /// </summary>
    public DateTime? Date { get; init; }

    /// <summary>
    /// Gets or sets the flags mask, for flags operators, or for equality
    /// operators applied to <see cref="IndexQueryField.Flags"/>.
    /// </summary>
    public int? Flags { get; init; }

    /// <summary>
    /// Gets or sets the minimum similarity threshold (0-1) for
    /// <see cref="IndexQueryOperator.Similar"/>.
    /// </summary>
    public double? Threshold { get; init; }

    /// <summary>
    /// Gets or sets the 0-based position of the clause in the query text.
    /// </summary>
    public int Position { get; init; }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString() =>
        $"[{(Field == IndexQueryField.Pin ? "@" + PinName : Field.ToString())}" +
        $" {Operator} {Value}]";
}
