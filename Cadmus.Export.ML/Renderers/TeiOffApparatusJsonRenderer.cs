using Cadmus.Philology.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Xml.Linq;
using Proteus.Core.Text;
using Fusi.Tools.Data;
using Cadmus.Core;
using Proteus.Rendering;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// JSON renderer for standoff TEI apparatus layer part. This works in tandem
/// with <see cref="TeiOffLinearTextTreeRenderer"/>, whose task is building the
/// base text referenced by the apparatus entries.
/// </summary>
/// <seealso cref="Export.Renderers.CadmusJsonRenderer" />
/// <seealso cref="IJsonRenderer" />
[Tag("it.vedph.json-renderer.tei-off.apparatus")]
public sealed class TeiOffApparatusJsonRenderer : MLJsonRenderer,
    IJsonRenderer, IConfigurable<AppLinearTextTreeRendererOptions>
{
    private readonly JsonSerializerOptions _jsonOptions;
    private TeiAppHelper _tei;

    private AppLinearTextTreeRendererOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="TeiOffApparatusJsonRenderer"/>
    /// class.
    /// </summary>
    public TeiOffApparatusJsonRenderer()
    {
        _jsonOptions = new()
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
        };
        _options = new();
        _tei = new(_options);
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(AppLinearTextTreeRendererOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _tei = new TeiAppHelper(_options);
    }

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="json">The input JSON.</param>
    /// <param name="context">The optional renderer context.</param>
    /// <param name="tree">The optional text tree. This is used for layer
    /// fragments to get source IDs targeting the various portions of the
    /// text.</param>
    /// <returns>Rendered output.</returns>
    /// <exception cref="InvalidOperationException">null tree</exception>
    protected override string DoRender(string json,
        CadmusRendererContext context, TreeNode<ExportedSegment>? tree = null)
    {
        if (tree == null)
        {
            throw new InvalidOperationException("Text tree is required " +
                "for rendering standoff apparatus");
        }

        // configure the helper
        _tei.Configure(context, tree);

        // get the text part
        IPart? textPart = context.GetTextPart();
        if (textPart == null) return "";

        // read fragments array
        JsonNode? root = JsonNode.Parse(json);
        if (root == null) return "";
        ApparatusLayerFragment[]? fragments =
            root["fragments"].Deserialize<ApparatusLayerFragment[]>(_jsonOptions);
        if (fragments == null || context == null) return "";

        // div @xml:id="item<ID>"
        // get the root element name (usually div)
        XName rootName = _options.ResolvePrefixedName(_options.RootElement);

        XElement itemDiv = new(rootName,
            new XAttribute(NamespaceOptions.XML + "id",
            $"item{(context.Source as IItem)!.Id}"));

        // process each fragment
        for (int frIndex = 0; frIndex < fragments.Length; frIndex++)
        {
            ApparatusLayerFragment fr = fragments[frIndex];

            // div @type="TAG"
            XElement frDiv = new(NamespaceOptions.TEI + "div");
            if (!string.IsNullOrEmpty(fr.Tag))
                frDiv.SetAttributeValue("type", fr.Tag);
            itemDiv.Add(frDiv);

            foreach (ApparatusEntry entry in fr.Entries)
            {
                // div/app @n="INDEX + 1"
                XElement? app = _tei.BuildAppElement(
                    textPart.Id, fr, frIndex, true);
                if (app != null) frDiv.Add(app);
            }
        }

        return itemDiv.ToString(_options.IsIndented
            ? SaveOptions.OmitDuplicateNamespaces
            : SaveOptions.DisableFormatting | SaveOptions.OmitDuplicateNamespaces);
    }
}
