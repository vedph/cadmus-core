using Cadmus.Core;
using Cadmus.Export.Renderers;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Proteus.Core.Text;
using Proteus.Rendering;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// Standoff TEI text tree renderer. This renders the base text from a linear
/// tree into a TEI segmented text using <c>seg</c>, each with its mapped ID,
/// so that it can be targeted by annotations.
/// <para>Tag: <c>it.vedph.text-tree-renderer.tei-off-linear</c>.</para>
/// </summary>
[Tag("it.vedph.text-tree-renderer.tei-off-linear")]
public sealed class TeiOffLinearTextTreeRenderer : CadmusTextTreeRenderer,
    IConfigurable<XmlTextTreeRendererOptions>
{
    private XmlTextTreeRendererOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeiOffLinearTextTreeRenderer"/>
    /// class.
    /// </summary>
    public TeiOffLinearTextTreeRenderer()
    {
        _options = new XmlTextTreeRendererOptions();
    }

    /// <summary>
    /// Configures this renderer with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(XmlTextTreeRendererOptions options)
    {
        _options = options ?? new XmlTextTreeRendererOptions();
    }

    /// <summary>
    /// Renders the specified tree.
    /// </summary>
    /// <param name="tree">The tree.</param>
    /// <param name="context">The rendering context.</param>
    /// <returns>Rendition.</returns>
    /// <exception cref="ArgumentNullException">tree or context</exception>
    protected override string DoCadmusRender(TreeNode<ExportedSegment> tree,
        CadmusRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentNullException.ThrowIfNull(context);

        // get the root element name
        XName rootName = _options.ResolvePrefixedName(_options.RootElement);

        // get the block name
        string blockType = "default";
        if (context.Data.TryGetValue(
            XmlTextTreeRendererOptions.CONTEXT_BLOCK_TYPE_KEY,
            out object? value))
        {
            blockType = value as string ?? "default";
        }

        XName blockName = _options.ResolvePrefixedName(
            _options.BlockElements[blockType]);

        // create root element like div @n="ITEM_NR
        XElement root = new(rootName);
        if (context.Data.TryGetValue(ItemComposer.M_ITEM_NR, out object? nr))
            root.SetAttributeValue("n", nr);

        // create block element like p
        int y = 1;
        XElement block = new(blockName,
            new XAttribute("source",
                TeiItemComposer.ITEM_ID_PREFIX + (context.Source as IItem)!.Id),
            new XAttribute("n", 1));
        root.Add(block);

        // traverse nodes and build the XML (each node corresponds to a fragment)
        tree.Traverse(node =>
        {
            if (node.Data?.Text != null)
            {
                AnnotatedTextRange? range = CadmusTextTreeBuilder
                    .GetSegmentFirstRange(node.Data);

                if (range?.FragmentIds?.Count > 0)
                {
                    int id = context.MapSourceId("seg",
                        $"{(context.Source as IItem)!.Id}/{node.Id}");

                    XElement seg = new(NamespaceOptions.TEI + "seg",
                        new XAttribute(NamespaceOptions.XML + "id", $"seg{id}"),
                        node.Data.Text);

                    block.Add(seg);
                }
                else
                {
                    block.Add(node.Data.Text);
                }
            }

            // open a new block if needed
            if (node.Data?.HasFeature(ExportedSegment.F_EOL_TAIL) == true)
            {
                block = new XElement(blockName,
                    new XAttribute("source",
                        TeiItemComposer.ITEM_ID_PREFIX +
                        (context.Source as IItem)!.Id),
                    new XAttribute("n", ++y));
                root.Add(block);
            }

            return true;
        });

        string xml = _options.IsRootIncluded
            ? root.ToString(_options.IsIndented
                ? SaveOptions.OmitDuplicateNamespaces
                : SaveOptions.OmitDuplicateNamespaces |
                  SaveOptions.DisableFormatting)
            : string.Concat(root.Nodes().Select(
            node => node.ToString(_options.IsIndented
            ? SaveOptions.OmitDuplicateNamespaces
            : SaveOptions.OmitDuplicateNamespaces |
                SaveOptions.DisableFormatting)));

        // if there is a pending group ID:
        // - if there is a current group, prepend tail.
        // - prepend head.
        return WrapXml(xml, context);
    }
}
