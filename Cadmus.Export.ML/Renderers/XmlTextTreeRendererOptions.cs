using Proteus.Core.Text;
using Proteus.Text.Xml;
using System.Collections.Generic;

namespace Cadmus.Export.ML.Renderers;

/// <summary>
/// Base class for XML text tree renderers options. Usually an XML tree renderer
/// outputs a single root XML element with rendered content. This is the
/// <see cref="RootElement"/>. The <see cref="BlockElements"/> property allows
/// to specify the element name(s) to be used for block elements inside it.
/// </summary>
public class XmlTextTreeRendererOptions : XmlTextFilterOptions
{
    /// <summary>
    /// The key used in the rendering context to keep track of unique identifiers
    /// for text segments like <c>seg</c>, <c>lem</c>, <c>rdg</c>, etc.
    /// </summary>
    public const string CONTEXT_SEG_IDKEY = "seg";

    /// <summary>
    /// The context block type key to retrieve it from the context data.
    /// </summary>
    public const string CONTEXT_BLOCK_TYPE_KEY = "block-type";

    /// <summary>
    /// Gets or sets the name of the root element. The default is <c>tei:div</c>.
    /// This is usually not rendered in output, but it is used as the root of
    /// the XML fragment built by the renderer.
    /// </summary>
    public string RootElement { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <see cref="RootElement"/>
    /// should be included in the output. The default is <c>false</c>.
    /// </summary>
    /// <value>
    ///   <c>true</c> if this instance is root included; otherwise, <c>false</c>.
    /// </value>
    public bool IsRootIncluded { get; set; }

    /// <summary>
    /// Gets or sets the block element name(s). The default name is "tei:p" under
    /// a <c>default</c> key, other names can be specified for conditional
    /// element names (e.g. when dealing with poetry rather than prose).
    /// If you need to specify a namespaced name, use the format "prefix:name"
    /// and define the prefix in the <see cref="NamespaceOptions.Namespaces"/>
    /// property.
    /// </summary>
    public IDictionary<string, string> BlockElements { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the XML output should be
    /// indented. This can be useful for diagnostic purposes.
    /// </summary>
    public bool IsIndented { get; set; }

    /// <summary>
    /// Gets or sets the head code template to be rendered at the start of the
    /// each group of items. Its value can include placeholders in curly braces,
    /// corresponding to any of the metadata keys defined in the item composer's
    /// context.
    /// </summary>
    public string? GroupHeadTemplate { get; set; }

    /// <summary>
    /// Gets or sets the tail code template to be rendered at the end of each
    /// group of items. Its value can include placeholders in curly braces,
    /// corresponding to any of the metadata keys defined in the item composer's
    /// context.
    /// </summary>
    public string? GroupTailTemplate { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="XmlTextTreeRendererOptions"/>
    /// class.
    /// </summary>
    public XmlTextTreeRendererOptions()
    {
        DefaultNsPrefix = "tei";
        RootElement = "tei:div";
        BlockElements = new Dictionary<string, string>
        {
            ["default"] = "tei:p"
        };
    }
}
