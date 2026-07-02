using Cadmus.Core;
using Fusi.Tools.Data;
using Proteus.Rendering;
using System;
using System.Text.Json;

namespace Cadmus.Export.ML;

/// <summary>
/// Base class for TEI standoff item composers. This deals with text items,
/// using an <see cref="ITextPartFlattener"/> to flatten it with all its
/// layers, and an <see cref="ITextTreeRenderer{String}"/> to render the resulting
/// text blocks into XML. It then uses a number of <see cref="IJsonRenderer"/>'s
/// to render each layer's fragment in its own XML document. So, ultimately
/// this produces several XML documents, one for the base text and as many
/// documents as its layers.
/// </summary>
/// <seealso cref="ItemComposer" />
public abstract class TeiOffItemComposer : ItemComposer
{
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// The text flow metadata key (<c>flow-key</c>).
    /// </summary>
    public const string M_FLOW_KEY = "flow-key";

    /// <summary>
    /// The layer identifier (<c>layer-id</c>). This is from the renderer
    /// context layer ID mappings.
    /// </summary>
    public const string M_LAYER_ID = "layer-id";

    /// <summary>
    /// Initializes a new instance of the <see cref="TeiOffItemComposer"/>
    /// class.
    /// </summary>
    protected TeiOffItemComposer()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };
    }

    /// <summary>
    /// Composes the output from the specified item.
    /// </summary>
    /// <returns>Composition result or null.</returns>
    protected override void DoCompose()
    {
        if (Output == null || TextTreeRenderer == null || Context.Source == null)
            return;

        IItem item = (IItem)Context.Source;

        // build text tree
        TreeNode<ExportedSegment>? tree = BuildTextTree(item);
        if (tree == null) return;

        // render text from tree
        string? result = TextTreeRenderer.Render(tree, Context);
        if (!string.IsNullOrEmpty(result))
            WriteOutput(PartBase.BASE_TEXT_ROLE_ID, result);

        // render layers
        foreach (IPart layerPart in GetLayerParts(item))
        {
            // get the renderer for the layer type
            string id = $"{layerPart.TypeId}" +
                $"{CadmusPreviewer.RENDERER_KEY_SEPARATOR}{layerPart.RoleId}";
            if (JsonRenderers.TryGetValue(id, out IJsonRenderer? renderer))
            {
                // render layer
                string json = JsonSerializer.Serialize(layerPart, _jsonOptions);
                result = renderer.Render(json, Context, tree);

                // write output
                WriteOutput(layerPart.RoleId!, result);
            }
        }
    }
}

#region TeiStandoffItemComposerOptions
/// <summary>
/// Base options for TEI standoff item composers.
/// </summary>
public class TeiOffItemComposerOptions
{
    /// <summary>
    /// Gets or sets the optional text head. This is written at the start
    /// of the text flow. Its value can include placeholders in curly
    /// braces, corresponding to any of the metadata keys defined in
    /// the item composer's context.
    /// </summary>
    public string? TextHead { get; set; }

    /// <summary>
    /// Gets or sets the optional text tail. This is written at the end
    /// of the text flow. Its value can include placeholders in curly
    /// braces, corresponding to any of the metadata keys defined in
    /// the item composer's context.
    /// </summary>
    public string? TextTail { get; set; }

    /// <summary>
    /// Gets or sets the optional layer head. This is written at the start
    /// of each layer flow. Its value can include placeholders in curly
    /// braces, corresponding to any of the metadata keys defined in
    /// the item composer's context.
    /// </summary>
    public string? LayerHead { get; set; }

    /// <summary>
    /// Gets or sets the optional layer tail. This is written at the end
    /// of each layer flow. Its value can include placeholders in curly
    /// braces, corresponding to any of the metadata keys defined in
    /// the item composer's context.
    /// </summary>
    public string? LayerTail { get; set; }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="TeiOffItemComposerOptions"/> class.
    /// </summary>
    public TeiOffItemComposerOptions()
    {
        TextHead = "<body>";
        TextTail = "</body>";
        LayerHead = "<TEI xmlns=\"http://www.tei-c.org/ns/1.0\">" +
            Environment.NewLine +
            "<standOff type=\"{" +
            ItemComposer.M_ITEM_NR + "}\">";
        LayerTail = "</standOff>" + Environment.NewLine + "</TEI>";
    }
}
#endregion
