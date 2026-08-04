using System;
using System.Collections.Generic;
using System.Text;

namespace Cadmus.Export.Json;

/// <summary>
/// A minimal placeholder template parser supporting metadata
/// (<c>{$...}</c>), data expression (<c>{@...}</c>), and macro
/// (<c>{!...}</c>) placeholders, used by <see cref="JsonTemplateNodeMapper"/>
/// to resolve <see cref="Cadmus.Export.Mapping.NodeMapping.Sid"/> and
/// <see cref="JsonNodeMapping.TargetProperty"/> templates. This purposely
/// does not support a context-node placeholder type, as there is no graph
/// context here (unlike the analogous parser used by the RDF-oriented
/// <c>JsonGraphNodeMapper</c> in Cadmus.Graph).
/// </summary>
internal sealed class PlaceholderTree
{
    // these chars correspond to PlaceholderNodeType enum
    private const string FNCHARS = "-$@!";

    private readonly List<PlaceholderNode> _fnNodes;

    /// <summary>
    /// The root node of this tree.
    /// </summary>
    public PlaceholderNode Root { get; }

    private PlaceholderTree()
    {
        _fnNodes = [];
        Root = new PlaceholderNode();
    }

    private static void ConsumeLiteral(StringBuilder sb, PlaceholderNode parent)
    {
        if (sb.Length > 0)
        {
            parent.AddChild(new PlaceholderNode { Value = sb.ToString() });
            sb.Clear();
        }
    }

    /// <summary>
    /// Adds the function node to the list of function nodes, which is
    /// sorted by descending depth and then by insertion order.
    /// </summary>
    /// <param name="node">The node.</param>
    private void AddFnNode(PlaceholderNode node)
    {
        int i = 0;
        while (i < _fnNodes.Count && node.Depth <= _fnNodes[i].Depth) i++;
        _fnNodes.Insert(i, node);
    }

    /// <summary>
    /// Creates a tree from the specified template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <returns>Tree.</returns>
    /// <exception cref="ArgumentNullException">template</exception>
    /// <exception cref="InvalidOperationException">invalid template</exception>
    public static PlaceholderTree Create(string template)
    {
        ArgumentNullException.ThrowIfNull(template);

        PlaceholderTree tree = new();
        PlaceholderNode node = tree.Root;

        // a literal template is a corner case, be performance-wise
        if (!template.Contains('{'))
        {
            node.AddChild(new PlaceholderNode { Value = template });
            return tree;
        }

        StringBuilder sb = new();
        int i = 0;
        int fnDepth = 0;

        while (i < template.Length)
        {
            switch (template[i])
            {
                case '\\':
                    // \{ and \} are escapes for { and }
                    if (i + 1 < template.Length &&
                        (template[i + 1] == '{' || template[i + 1] == '}'))
                    {
                        sb.Append(template[i + 1]);
                        i += 2;
                    }
                    else
                    {
                        sb.Append(template[i]);
                        i++;
                    }
                    break;

                case '{':
                    if (i + 1 == template.Length)
                    {
                        sb.Append('{');
                        i++;
                        break;
                    }
                    int j = FNCHARS.IndexOf(template[i + 1]);
                    if (j == -1)
                    {
                        throw new InvalidOperationException(
                            "Invalid template placeholder type " +
                            $"'{template[i + 1]}' in \"{template}\"");
                    }
                    fnDepth++;
                    ConsumeLiteral(sb, node);
                    PlaceholderNode fn = new()
                    {
                        Type = (PlaceholderNodeType)j,
                    };
                    node.AddChild(fn);
                    node = fn;
                    tree.AddFnNode(fn);
                    i += 2;
                    break;

                case '}':
                    if (fnDepth == 0)
                    {
                        sb.Append('}');
                        i++;
                        break;
                    }
                    fnDepth--;
                    ConsumeLiteral(sb, node);
                    node = node.Parent!;
                    i++;
                    break;

                default:
                    sb.Append(template[i++]);
                    break;
            }
        }
        if (fnDepth > 0)
        {
            throw new InvalidOperationException(
                $"Invalid template, check braces: \"{template}\"");
        }
        ConsumeLiteral(sb, node);

        return tree;
    }

    /// <summary>
    /// Resolves this template.
    /// </summary>
    /// <param name="fnResolver">The function node resolver function to
    /// use. This gets a function node, and should evaluate all (and only)
    /// its direct children, returning a string result.</param>
    /// <returns>Resolved template.</returns>
    /// <exception cref="ArgumentNullException">fnResolver</exception>
    public string Resolve(Func<PlaceholderNode, string> fnResolver)
    {
        ArgumentNullException.ThrowIfNull(fnResolver);

        // resolve all the fn nodes from the deepest ones
        foreach (PlaceholderNode fn in _fnNodes) fn.Value = fnResolver(fn);

        // build the output from the root's direct children, as all the fn
        // nodes have been resolved and thus flattened into them
        StringBuilder sb = new();
        foreach (PlaceholderNode child in Root.Children) sb.Append(child.Value);

        return sb.ToString();
    }
}
