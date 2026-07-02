using DevLab.JmesPath;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Fusi.Xml;
using Fusi.Xml.Extras.Render;
using Proteus.Rendering;
using System;
using System.Collections.Generic;
using System.Xml.Linq;

namespace Cadmus.Export.Renderers;

/// <summary>
/// XSLT based JSON renderer. This is one of the most customizable renderers,
/// using an optional pipeline of JMESPath transforms to preprocess the
/// JSON input, and an XSLT script to render it once converted to XML.
/// <para>Once transformations have been processed, you also have the option
/// of converting regions of Markdown code in the result (or the whole result)
/// into HTML or plain text.</para>
/// <para>Tag: <c>it.vedph.json-renderer.xslt</c>.</para>
/// </summary>
[Tag("it.vedph.json-renderer.xslt")]
public sealed class XsltJsonRenderer : CadmusJsonRenderer, IJsonRenderer,
    IConfigurable<XsltJsonRendererOptions>
{
    // https://jmespath.org/tutorial.html
    // https://github.com/jdevillard/JmesPath.Net
    // https://www.newtonsoft.com/json/help/html/ConvertingJSONandXML.htm

    private readonly JsonToXmlConverter _jsonToXmlConverter;
    private XsltJsonRendererOptions? _options;
    private XsltTransformer? _transformer;

    /// <summary>
    /// Initializes a new instance of the <see cref="XsltJsonRenderer"/> class.
    /// </summary>
    public XsltJsonRenderer()
    {
        _jsonToXmlConverter = new();
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(XsltJsonRendererOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options;

        if (!string.IsNullOrWhiteSpace(options.Xslt))
            _transformer = new XsltTransformer(options.Xslt);
        else
            _transformer = null;
    }

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="json">The input JSON. When using JMESPath expressions,
    /// this will be automatically wrapped into a root object with a single
    /// <c>root</c> property whose value is the received JSON code.</param>
    /// <param name="context">The optional renderer context.</param>
    /// <returns>Rendered output.</returns>
    /// <param name="tree">The optional text tree. This is used for layer
    /// fragments to get source IDs targeting the various portions of the
    /// text.</param>
    /// <exception cref="ArgumentNullException">json</exception>
    protected override string DoRender(string json,
        CadmusRendererContext context, TreeNode<ExportedSegment>? tree = null)
    {
        if (_options == null) return json;

        ArgumentNullException.ThrowIfNull(json);
        if (!_options.FrDecoration &&
            _transformer == null &&
            _options.JsonExpressions?.Count == 0)
        {
            return "";
        }

        // wrap in root for JMESPath expressions to work with root.* paths
        bool needsWrapping = _options.JsonExpressions?.Count > 0;
        if (needsWrapping)
        {
            json = "{\"root\":" + json + "}";
        }

        // decorate if requested
        if (_options.FrDecoration)
            json = JsonDecorator.DecorateLayerPartFrr(json);

        // transform JSON if requested
        if (_options.JsonExpressions?.Count > 0)
        {
            JmesPath jmes = new();
            foreach (string e in _options.JsonExpressions)
            {
                json = jmes.Transform(json, e);
            }
            if (_options.QuoteStripping && json.Length > 1
                && json[0] == '"' && json[^1] == '"')
            {
                json = json[1..^1];
            }
        }

        // transform via XSLT if required
        if (_transformer != null)
        {
            // convert to XML
            XElement root = _jsonToXmlConverter.Convert(json, _options);

            // transform via XSLT
            return _transformer.Transform(root.OuterXml());
        }

        return json;
    }
}

#region XsltJsonRendererOptions
/// <summary>
/// Options for <see cref="XsltJsonRenderer"/>.
/// </summary>
public class XsltJsonRendererOptions : JsonToXmlConverterOptions
{
    /// <summary>
    /// Gets or sets a value indicating whether fragment decoration
    /// is enabled. When true, the JSON corresponding to a layer part
    /// gets an additional <c>_key</c> property in each of its <c>fragments</c>
    /// array items. This key is built from the layer type ID eventually
    /// followed by <c>|</c> plus the role ID, followed by the fragment's
    /// index.
    /// </summary>
    public bool FrDecoration { get; set; }

    /// <summary>
    /// Gets or sets the JSON transform expressions using JMES Path.
    /// </summary>
    public IList<string> JsonExpressions { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether quotes wrapping a string
    /// result should be removed once JSON transforms have completed.
    /// </summary>
    public bool QuoteStripping { get; set; }

    /// <summary>
    /// Gets or sets the XSLT script used to produce the final result.
    /// </summary>
    public string? Xslt { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XsltJsonRendererOptions"/> class.
    /// </summary>
    public XsltJsonRendererOptions()
    {
        DefaultNsPrefix = null;
    }
}
#endregion
