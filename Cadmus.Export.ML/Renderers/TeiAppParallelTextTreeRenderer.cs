using Cadmus.Core;
using Cadmus.Export.Renderers;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using MongoDB.Driver;
using Proteus.Core.Text;
using Proteus.Rendering;
using Proteus.Text.Xml;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// TEI parallel segmentation tree with single apparatus layer renderer.
/// <para>Tag: <c>it.vedph.text-tree-renderer.tei-app-parallel</c>.</para>
/// </summary>
[Tag("it.vedph.text-tree-renderer.tei-app-parallel")]
public sealed class TeiAppParallelTextTreeRenderer : CadmusTextTreeRenderer,
    IConfigurable<TeiAppParallelTextTreeRendererOptions>
{
    private TeiAppHelper _tei;
    private TeiAppParallelTextTreeRendererOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeiAppParallelTextTreeRenderer"/>
    /// class.
    /// </summary>
    public TeiAppParallelTextTreeRenderer()
    {
        _options = new();
        _tei = new TeiAppHelper(_options);
    }

    /// <summary>
    /// Configures this renderer with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(TeiAppParallelTextTreeRendererOptions options)
    {
        _options = options ?? new TeiAppParallelTextTreeRendererOptions();
        _tei = new TeiAppHelper(_options);

        GroupHeadTemplate = _options.GroupHeadTemplate;
        GroupTailTemplate = _options.GroupTailTemplate;
    }

    private static void ProcessVariants(Dictionary<string, HashSet<string>> variants,
        string originalText, XElement targetElem)
    {
        if (variants.Count == 0) return;

        // for single variant we just output its text
        if (variants.Count == 1)
        {
            targetElem.Add(variants.Keys.First());
        }

        // for multiple variants, add as many child elements of type lem or rdg
        // using lem only when the variant's text is original
        else
        {
            // app
            XElement app = new(NamespaceOptions.TEI + "app");
            targetElem.Add(app);

            // for each node, add an entry
            foreach (string text in variants.Keys)
            {
                XElement entryElem = text == originalText
                    ? new(NamespaceOptions.TEI + "lem", text)
                    : new(NamespaceOptions.TEI + "rdg", text);
                app.Add(entryElem);

                // TODO wit and resp attrs from variants[text]
            }
        }
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
        //TokenTextLayerPart<ApparatusLayerFragment>? layerPart =
        //    (context.Source as IItem)!.Parts.FirstOrDefault(p =>
        //        p.TypeId == "it.vedph.token-text-layer" &&
        //        p.RoleId == "fr.it.vedph.apparatus")
        //    as TokenTextLayerPart<ApparatusLayerFragment>;

        // create root element
        XElement root = new(rootName);
        XElement block = new(blockName,
            _options.NoItemSource
                ? null
                : new XAttribute("source",
                    TeiItemComposer.ITEM_ID_PREFIX +
                        (context.Source as IItem)!.Id),
            new XAttribute("n", 1));
        root.Add(block);

        // traverse nodes collecting text variants with their version tags
        int y = 2;
        Dictionary<string, HashSet<string>> textVariants = [];
        string? originalText = null;

        // traverse breadth-first so we can group nodes by their Y level
        tree.Traverse(node =>
        {
            // skip blank nodes (root)
            if (node.Data?.Text == null) return true;

            // while the node belongs to the current level, add it to the dictionary
            if (node.GetY() == y)
            {
                // set original text for this group (all the nodes in group
                // should have it equal)
                AnnotatedTextRange? range =
                    CadmusTextTreeBuilder.GetSegmentFirstRange(node.Data);
                originalText ??= range?.Text;

                if (!textVariants.TryGetValue(node.Data.Text,
                    out HashSet<string>? tags))
                {
                    tags = [];
                    textVariants[node.Data.Text] = tags;
                }

                if (node.Data.Features?.Count > 0)
                {
                    foreach (string tag in node.Data.Features.Where(
                        f => f.Name == "tag").Select(f => f.Value!))
                    {
                        tags.Add(tag);
                    }
                }
            }
            // otherwise, process the current level nodes and start a new group
            else
            {
                ProcessVariants(textVariants, originalText!, block);
                textVariants.Clear();
                if (node.Data.Features?.Count > 0)
                {
                    foreach (string tag in node.Data.Features.Where(
                        f => f.Name == "tag").Select(f => f.Value!))
                    {
                        if (!textVariants.ContainsKey(node.Data.Text!))
                            textVariants[node.Data.Text] = [];
                        textVariants[node.Data.Text].Add(tag);
                    }
                }
                y = node.GetY();
            }
            return true;
        }, true);

        // process the last group
        ProcessVariants(textVariants, originalText!, block);

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
public class TeiAppParallelTextTreeRendererOptions : XmlTextTreeRendererOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether to omit item source in the
    /// output XML. Item source can be used either for diagnostic purposes
    /// or for resolving links in a filter. If you don't need them and you
    /// want a smaller XML you can set this to true.
    /// </summary>
    public bool NoItemSource { get; set; }
}
