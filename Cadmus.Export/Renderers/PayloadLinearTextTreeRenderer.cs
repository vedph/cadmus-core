using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Proteus.Rendering;
using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadmus.Export.Renderers;

/// <summary>
/// A renderer which outputs a string representing a JSON array, using as
/// items the nodes payloads from a linear tree. The array may directly
/// include payloads as items, or (default) arrays which in turn include payloads,
/// where each inner array represents a line of the original text.
/// This is used for single branch trees and the output is typically targeted
/// to frontend UI components which should render rows of blocks (or just blocks)
/// of text with links to fragments.
/// <para>Tag: <c>it.vedph.text-tree-renderer.payload-linear</c>.</para>
/// </summary>
/// <seealso cref="CadmusTextTreeRenderer" />
/// <seealso cref="ICadmusTextTreeRenderer" />
[Tag("it.vedph.text-tree-renderer.payload-linear")]
public sealed class PayloadLinearTextTreeRenderer : CadmusTextTreeRenderer,
    IConfigurable<PayloadLinearTextTreeRendererOptions>
{
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private bool _flatten;

    /// <summary>
    /// Configures this renderer with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException"></exception>
    public void Configure(PayloadLinearTextTreeRendererOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _flatten = options.FlattenLines;
    }

    /// <summary>
    /// Renders the specified tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="context">The rendering context.</param>
    /// <returns>Rendition.</returns>
    /// <exception cref="ArgumentNullException"></exception>
    protected override string DoCadmusRender(TreeNode<ExportedSegment> tree,
        CadmusRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(context);

        if (!tree.HasChildren) return "[]";

        StringBuilder sb = new();
        sb.Append('[');

        bool inner = false;
        TreeNode<ExportedSegment>? node = tree.Children[0];
        do
        {
            if (!_flatten && !inner)
            {
                // open inner array
                sb.Append('[');
                inner = true;
            }

            // add data
            sb.Append(node.Data != null
                ? JsonSerializer.Serialize(node.Data, _jsonOptions) : "{}");

            // close inner array if EOL
            if (!_flatten && node.Data?
                .HasFeature(ExportedSegment.F_EOL_TAIL) == true)
            {
                sb.Append(']');
                inner = false;
            }

            node = node.HasChildren? node.Children[0] : null;
            if (node != null) sb.Append(',');
        } while (node != null);

        sb.Append(']');
        if (inner && !_flatten) sb.Append(']');

        return sb.ToString();
    }
}

/// <summary>
/// Options for <see cref="PayloadLinearTextTreeRenderer"/>
/// </summary>
public class PayloadLinearTextTreeRendererOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether the renderer should flatten
    /// the original lines, i.e. do not render an array of arrays, where each
    /// node is inside the inner array up to the end of the line, but rather
    /// an array of nodes, discarding line breaks.
    /// </summary>
    public bool FlattenLines { get; set; }
}
