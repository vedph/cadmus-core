using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using Fusi.Tools.Data;
using Proteus.Core.Text;
using Proteus.Rendering;
using System;
using System.Text;
using System.Xml.Linq;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// TEI app element rendition helper.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="TeiAppHelper"/> class.
/// </remarks>
/// <param name="options">The options.</param>
/// <exception cref="ArgumentNullException">options</exception>
public class TeiAppHelper(XmlTextTreeRendererOptions options)
{
    private readonly XmlTextTreeRendererOptions _options =
        options ?? throw new ArgumentNullException(nameof(options));

    private CadmusRendererContext? _context;
    private TreeNode<ExportedSegment>? _tree;

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
    /// Configures the specified context.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="tree">The tree.</param>
    /// <exception cref="ArgumentNullException">context or tree</exception>
    public void Configure(CadmusRendererContext context,
        TreeNode<ExportedSegment> tree)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _tree = tree ?? throw new ArgumentNullException(nameof(tree));
    }

    private void EnsureConfigured()
    {
        if (_context == null)
            throw new InvalidOperationException("Context not set for TEI helper");

        if (_tree == null)
            throw new InvalidOperationException("Tree not set for TEI helper");
    }

    private void AddWitDetail(string attrName, string? witOrResp,
        string sourceId, string detail, XElement lemOrRdg)
    {
        // witDetail
        XElement witDetail = new(NamespaceOptions.TEI + "witDetail", detail);
        if (WitDetailAsChild) lemOrRdg.Add(witDetail);
        else lemOrRdg.Parent!.Add(witDetail);

        // @target=lem or rdg ID
        string local = lemOrRdg.Name.LocalName;
        int targetId = _context!.MapSourceId(local, sourceId);
        lemOrRdg.SetAttributeValue(_options.ResolvePrefixedName("xml:id"),
            $"{local}{targetId}");
        witDetail.SetAttributeValue("target", $"#{local}{targetId}");

        // @wit or @resp
        if (witOrResp != null)
            witDetail.SetAttributeValue(attrName, $"#{witOrResp}");
    }

    private void AddWitOrResp(string sourceId, ApparatusEntry entry,
        XElement lemOrRdg)
    {
        StringBuilder wit = new();
        StringBuilder resp = new();

        foreach (AnnotatedValue av in entry.Witnesses)
        {
            if (wit.Length > 0) wit.Append(' ');
            wit.Append('#').Append(av.Value);
            if (!string.IsNullOrEmpty(av.Note))
                AddWitDetail("wit", av.Value, sourceId, av.Note, lemOrRdg);
        }

        foreach (LocAnnotatedValue lav in entry.Authors)
        {
            if (resp.Length > 0) resp.Append(' ');
            resp.Append('#').Append(lav.Value);
            if (!string.IsNullOrEmpty(lav.Note))
                AddWitDetail("resp", lav.Value, sourceId, lav.Note, lemOrRdg);
        }

        if (wit.Length > 0)
            lemOrRdg.SetAttributeValue("wit", wit.ToString());
        if (resp.Length > 0)
            lemOrRdg.SetAttributeValue("resp", resp.ToString());
    }

    /// <summary>
    /// Builds the <c>app</c> element from the specified apparatus fragment.
    /// </summary>
    /// <param name="textPartId">The text part identifier.</param>
    /// <param name="fragment">The apparatus fragment</param>
    /// <param name="frIndex">Index of the fragment in its layer part.</param>
    /// <param name="addLoc">True to add location to the app element (usually
    /// for standoff).</param>
    /// <param name="zeroVarType">The value for the type attribute to add to
    /// <c>rdg</c> elements for zero-variants, i.e. variants with no text
    /// meaning an omission. If null, no attribute will be added.</param>
    /// <returns>The element built.</returns>
    /// <exception cref="ArgumentNullException">textPartId or fr</exception>
    public XElement? BuildAppElement(string textPartId,
        ApparatusLayerFragment fragment, int frIndex, bool addLoc,
        string? zeroVarType = null)
    {
        ArgumentNullException.ThrowIfNull(textPartId);
        ArgumentNullException.ThrowIfNull(fragment);

        EnsureConfigured();

        // calculate the apparatus fragment ID prefix
        // (like "it.vedph.token-text-layer:fr.it.vedph.comment@INDEX")
        string prefix = CadmusTextTreeBuilder.GetFragmentPrefixFor(
            new TokenTextLayerPart<ApparatusLayerFragment>()) + frIndex;

        // find first and last nodes having a fragment ID starting with prefix
        var bounds = MLJsonRenderer.FindFragmentBounds(prefix, _tree!);
        if (bounds == null) return null;

        // collect text from nodes
        StringBuilder text = new();
        bounds.Value.First.Traverse(node =>
        {
            if (node.Data != null) text.Append(node.Data.Text);
            return node != bounds.Value.Last;
        });

        // app @n="FRINDEX+1"
        XElement app = new(NamespaceOptions.TEI + "app");
        if (!NoNAttribute)
            app.SetAttributeValue("n", frIndex + 1);

        // app @type="TAG"
        if (!string.IsNullOrEmpty(fragment.Tag))
            app.SetAttributeValue("type", fragment.Tag);

        // app @loc="segID" or loc @spanFrom/spanTo
        if (addLoc)
        {
            MLJsonRenderer.AddTeiLocToElement(
                bounds.Value.First, bounds.Value.Last, app, _context!);
        }

        // for each entry
        int entryIndex = 0;
        foreach (ApparatusEntry entry in fragment.Entries)
        {
            // if it has a variant render rdg, else render lem
            XElement lemOrRdg = entry.IsAccepted
                ? new(NamespaceOptions.TEI + "lem", text.ToString())
                : new(NamespaceOptions.TEI + "rdg", entry.Value);

            // add @type if tag
            if (!string.IsNullOrEmpty(entry.Tag))
                lemOrRdg.SetAttributeValue("type", entry.Tag);

            // add @type if zero variant
            if (zeroVarType != null && (string.IsNullOrEmpty(entry.Value)))
            {
                string? type = lemOrRdg.Attribute("type")?.Value;
                if (type != null)
                    lemOrRdg.SetAttributeValue("type", type + " " + zeroVarType);
                else
                    lemOrRdg.SetAttributeValue("type", zeroVarType);
            }

            // rdg or lem @n="ENTRY_INDEX+1"
            if (!NoNAttribute)
                lemOrRdg.SetAttributeValue("n", entryIndex + 1);
            app.Add(lemOrRdg);

            // rdg or lem @type="TAG"
            if (!string.IsNullOrEmpty(entry.Tag))
                lemOrRdg.SetAttributeValue("type", entry.Tag);

            // rdg or lem/note
            if (!string.IsNullOrEmpty(entry.Note))
            {
                lemOrRdg.Add(new XElement(NamespaceOptions.TEI + "note",
                    entry.Note));
            }

            // rdg or lem/@wit or @resp
            AddWitOrResp($"{textPartId}/{frIndex}.{entryIndex}", entry,
                lemOrRdg);

            entryIndex++;
        }

        return app;
    }
}
