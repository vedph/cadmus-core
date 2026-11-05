using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Index.Search;

/// <summary>
/// A node in a parsed index query. This is the abstract syntax tree
/// produced by <see cref="IndexQueryParser"/>, independent from any
/// specific storage technology.
/// </summary>
public abstract class IndexQueryNode
{
}

/// <summary>
/// The logical operator of an <see cref="IndexQueryLogicalNode"/>.
/// </summary>
public enum IndexQueryLogicalOperator
{
    /// <summary>All the children must match.</summary>
    And = 0,
    /// <summary>Any of the children must match.</summary>
    Or
}

/// <summary>
/// A logical AND or OR node with 2 or more children.
/// </summary>
/// <seealso cref="IndexQueryNode" />
public sealed class IndexQueryLogicalNode : IndexQueryNode
{
    /// <summary>
    /// Gets the logical operator.
    /// </summary>
    public IndexQueryLogicalOperator Operator { get; }

    /// <summary>
    /// Gets the children nodes.
    /// </summary>
    public IReadOnlyList<IndexQueryNode> Children { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexQueryLogicalNode"/>
    /// class.
    /// </summary>
    /// <param name="op">The operator.</param>
    /// <param name="children">The children.</param>
    /// <exception cref="ArgumentNullException">children</exception>
    public IndexQueryLogicalNode(IndexQueryLogicalOperator op,
        IEnumerable<IndexQueryNode> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        Operator = op;
        Children = children.ToList();
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString() => "(" + string.Join(
        Operator == IndexQueryLogicalOperator.And ? " AND " : " OR ",
        Children) + ")";
}

/// <summary>
/// A logical NOT node.
/// </summary>
/// <seealso cref="IndexQueryNode" />
public sealed class IndexQueryNotNode : IndexQueryNode
{
    /// <summary>
    /// Gets the negated node.
    /// </summary>
    public IndexQueryNode Operand { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="IndexQueryNotNode"/>
    /// class.
    /// </summary>
    /// <param name="operand">The operand.</param>
    /// <exception cref="ArgumentNullException">operand</exception>
    public IndexQueryNotNode(IndexQueryNode operand)
    {
        Operand = operand ?? throw new ArgumentNullException(nameof(operand));
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString() => $"NOT {Operand}";
}
