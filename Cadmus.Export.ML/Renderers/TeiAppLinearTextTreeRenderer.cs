using Cadmus.Core;
using Cadmus.Export.Renderers;
using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using MongoDB.Driver;
using Proteus.Rendering;
using Proteus.Text.Xml;
using System;
using System.Linq;
using System.Xml.Linq;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// TEI linear text tree with single apparatus layer renderer.
/// <para>Tag: <c>it.vedph.text-tree-renderer.tei-app-linear</c>.</para>
/// </summary>
[Tag("it.vedph.text-tree-renderer.tei-app-linear")]
public sealed class TeiAppLinearTextTreeRenderer : CadmusTextTreeRenderer,
    IConfigurable<AppLinearTextTreeRendererOptions>
{
    private TeiAppHelper _tei;

    private AppLinearTextTreeRendererOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeiAppLinearTextTreeRenderer"/>
    /// class.
    /// </summary>
    public TeiAppLinearTextTreeRenderer()
    {
        _options = new();
        _tei = new TeiAppHelper(_options);
    }

    /// <summary>
    /// Configures this renderer with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(AppLinearTextTreeRendererOptions options)
    {
        _options = options ?? new AppLinearTextTreeRendererOptions();
        _tei = new TeiAppHelper(_options)
        {
            NoNAttribute = options?.NoNAttribute ?? false,
            WitDetailAsChild = options?.WitDetailAsChild ?? false,
        };

        GroupHeadTemplate = _options.GroupHeadTemplate;
        GroupTailTemplate = _options.GroupTailTemplate;
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

        // configure the helper
        _tei.Configure(context, tree);

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

        // get text part
        IPart? textPart = context.GetTextPart();
        if (textPart == null) return "";    // should not happen

        // get apparatus layer part
        TokenTextLayerPart<ApparatusLayerFragment>? layerPart =
            (context.Source as IItem)!.Parts.FirstOrDefault(p =>
                p.TypeId == "it.vedph.token-text-layer" &&
                p.RoleId == "fr.it.vedph.apparatus")
            as TokenTextLayerPart<ApparatusLayerFragment>;

        // calculate the apparatus fragment ID prefix
        // (like "it.vedph.token-text-layer:fr.it.vedph.comment@")
        string? prefix = layerPart != null
            ? CadmusTextTreeBuilder.GetFragmentPrefixFor(layerPart) : null;

        // create root element
        IItem item = (IItem)context.Source;
        XElement root = new(rootName);
        XElement block = new(blockName,
            _options.NoItemSource
                ? null
                : new XAttribute("source",
                    TeiItemComposer.ITEM_ID_PREFIX + item.Id),
            _options.NoNAttribute
                ? null
                : new XAttribute("n", 1));
        root.Add(block);

        // traverse nodes and build the XML (each node corresponds to a fragment)
        int y = 1;
        tree.Traverse(node =>
        {
            string? frId = prefix != null
                ? CadmusTextTreeBuilder.GetFragmentIdWithPrefix(node.Data, prefix)
                : null;

            if (frId != null)
            {
                // get the index of the fragment linked to this node
                int frIndex = CadmusTextTreeBuilder.GetFragmentIndex(frId);

                // app
                XElement app = _tei.BuildAppElement(textPart.Id,
                    layerPart!.Fragments[frIndex], frIndex, false,
                    _options.ZeroVariantType)!;
                block.Add(app);
            }
            else
            {
                if (!string.IsNullOrEmpty(node.Data?.Text))
                    block.Add(node.Data.Text);
            }

            // open a new block if needed
            if (node.Data?.HasFeature(ExportedSegment.F_EOL_TAIL) == true)
            {
                block = new XElement(blockName,
                    _options.NoItemSource
                        ? null
                        : new XAttribute("source",
                            TeiItemComposer.ITEM_ID_PREFIX + item.Id),
                    _options.NoNAttribute
                        ? null
                        : new XAttribute("n", ++y));
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

/// <summary>
/// Options for <see cref="TeiAppLinearTextTreeRenderer"/>.
/// </summary>
/// <seealso cref="XmlTextFilterOptions" />
public class AppLinearTextTreeRendererOptions : XmlTextTreeRendererOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to omit item source in the
    /// output XML. Item source can be used either for diagnostic purposes
    /// or for resolving links in a filter. If you don't need them and you
    /// want a smaller XML you can set this to true.
    /// </summary>
    public bool NoItemSource { get; set; }

    /// <summary>
    /// Do not output @n attribute for <c>app</c>, <c>lem</c>, and <c>rdg</c>.
    /// </summary>
    public bool NoNAttribute { get; set; }

    /// <summary>
    /// True to render <c>witDetail</c> as a child of <c>lem</c>/<c>rdg</c>
    /// rather than as a sibling (which is the recommended option).
    /// </summary>
    public bool WitDetailAsChild { get; set; }

    /// <summary>
    /// Gets or sets the value for the type attribute to add to <c>rdg</c>
    /// elements for zero-variants, i.e. variants with no text meaning an
    /// omission. If null, no attribute will be added. The default is null.
    /// </summary>
    public string? ZeroVariantType { get; set; }
}
