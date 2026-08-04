using System;
using System.Collections.Generic;

namespace Cadmus.Export.Json;

/// <summary>
/// A node used to represent literals and functions in a placeholder
/// template resolved by <see cref="JsonTemplateNodeMapper"/> (e.g. a
/// <see cref="Cadmus.Export.Mapping.NodeMapping.Sid"/> or
/// <see cref="JsonNodeMapping.TargetProperty"/> template).
/// </summary>
/// <seealso cref="PlaceholderTree"/>
internal sealed class PlaceholderNode
{
    private List<PlaceholderNode>? _children;

    /// <summary>
    /// The optional parent of this node. Null if this is the root node.
    /// </summary>
    public PlaceholderNode? Parent { get; private set; }

    /// <summary>
    /// The optional children of this node.
    /// </summary>
    public IList<PlaceholderNode> Children => _children ??= [];

    /// <summary>
    /// Gets the children nodes count.
    /// </summary>
    public int ChildrenCount => _children?.Count ?? 0;

    /// <summary>
    /// Gets the depth level (1=root).
    /// </summary>
    public int Depth { get; private set; }

    /// <summary>
    /// The type of this node.
    /// </summary>
    public PlaceholderNodeType Type { get; set; }

    /// <summary>
    /// The literal value of this node. This is null if the node is a
    /// function node and has not yet been resolved.
    /// </summary>
    public string? Value { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PlaceholderNode"/> class.
    /// </summary>
    public PlaceholderNode()
    {
        Depth = 1;
    }

    /// <summary>
    /// Adds <paramref name="node"/> as the last child of this node.
    /// </summary>
    /// <param name="node">The node.</param>
    /// <exception cref="ArgumentNullException">node</exception>
    public void AddChild(PlaceholderNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        node.Parent = this;
        node.Depth = Depth + 1;
        Children.Add(node);
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    public override string ToString()
    {
        return $"[{Enum.GetName(Type)}] \"{Value}\" ({Children?.Count ?? 0})";
    }
}

/// <summary>
/// The type of <see cref="PlaceholderNode"/>.
/// </summary>
internal enum PlaceholderNodeType
{
    /// <summary>Literal.</summary>
    Literal = 0,
    /// <summary>Metadatum (<c>{$...}</c>).</summary>
    Metadatum,
    /// <summary>Data expression (<c>{@...}</c>).</summary>
    Expression,
    /// <summary>Macro (<c>{!...}</c>).</summary>
    Macro
}
