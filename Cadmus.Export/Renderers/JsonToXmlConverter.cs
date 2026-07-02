using Proteus.Core.Text;
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Cadmus.Export.Renderers;

/// <summary>
/// JSON to XML converter for XSLT processing. This converter transforms
/// JSON documents into XML with special handling for arrays and namespaces,
/// making the output suitable for XSLT transformations.
/// </summary>
public sealed class JsonToXmlConverter
{
    private readonly EnglishSingularizer _singularizer;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonToXmlConverter"/>
    /// class.
    /// </summary>
    public JsonToXmlConverter()
    {
        _singularizer = new EnglishSingularizer();
    }

    /// <summary>
    /// Converts the specified JSON string into an XML element.
    /// </summary>
    /// <param name="json">The JSON string to convert.</param>
    /// <param name="options">The optional conversion options.</param>
    /// <returns>The root XML element.</returns>
    /// <exception cref="ArgumentNullException">json</exception>
    /// <exception cref="JsonException">Invalid JSON format.</exception>
    public XElement Convert(string json,
        JsonToXmlConverterOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(json);

        options ??= new JsonToXmlConverterOptions();

        // Parse JSON
        JsonDocument doc = JsonDocument.Parse(json,
            new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

        // Build XML tree
        XName rootName = options.ResolvePrefixedName(
            options.RootElementName);
        XElement root = new(rootName);

        ProcessJsonElement(doc.RootElement, root, options);

        return root;
    }

    private XName GetArrayItemName(string arrayName,
        JsonToXmlConverterOptions options)
    {
        // Check if there's an explicit mapping
        if (options.WrappedEntryNames?.TryGetValue(arrayName,
            out string? itemName) == true)
        {
            // Resolve prefixed names (e.g. "tei:div")
            if (itemName.Contains(':'))
            {
                IXmlNamespaceResolver resolver = options.GetResolver();
                return NamespaceOptions.ResolvePrefixedName(
                    itemName, resolver, options.DefaultNsPrefix);
            }
            return XName.Get(itemName);
        }

        // Try singularization as fallback
        string? singularized = _singularizer.Singularize(arrayName);

        // If singularization fails or returns empty, use original name
        if (string.IsNullOrEmpty(singularized))
            return XName.Get(arrayName);

        return XName.Get(singularized);
    }

    private void ProcessJsonElement(JsonElement element,
        XElement parent, JsonToXmlConverterOptions options)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                ProcessJsonObject(element, parent, options);
                break;
            case JsonValueKind.Array:
                ProcessJsonArray(element, parent, options,
                    parent.Name.LocalName);
                break;
            default:
                ProcessJsonValue(element, parent);
                break;
        }
    }

    private void ProcessJsonObject(JsonElement element,
        XElement parent, JsonToXmlConverterOptions options)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (ShouldSkipProperty(property, options))
                continue;

            XName childName = XName.Get(property.Name);
            XElement child = new(childName);

            if (property.Value.ValueKind == JsonValueKind.Array)
            {
                ProcessJsonArray(property.Value, parent, options,
                    property.Name);
            }
            else
            {
                ProcessJsonElement(property.Value, child, options);
                parent.Add(child);
            }
        }
    }

    private void ProcessJsonArray(JsonElement element,
        XElement parent, JsonToXmlConverterOptions options,
        string arrayName)
    {
        // Create array container element
        XName containerName = XName.Get(arrayName);
        XElement container = new(containerName);

        // Get the name for individual items
        XName itemName = GetArrayItemName(arrayName, options);

        // Process each array item
        int index = 1;
        foreach (JsonElement item in element.EnumerateArray())
        {
            XElement itemElement = new(itemName);

            if (options.ArrayItemNumbering)
            {
                itemElement.SetAttributeValue("n", index);
            }

            ProcessJsonElement(item, itemElement, options);
            container.Add(itemElement);
            index++;
        }

        parent.Add(container);
    }

    private static void ProcessJsonValue(JsonElement element,
        XElement parent)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                parent.Value = element.GetString() ?? string.Empty;
                break;
            case JsonValueKind.Number:
                parent.Value = element.GetRawText();
                break;
            case JsonValueKind.True:
                parent.Value = "true";
                break;
            case JsonValueKind.False:
                parent.Value = "false";
                break;
            case JsonValueKind.Null:
                // leave empty
                break;
        }
    }

    private static bool ShouldSkipProperty(JsonProperty property,
        JsonToXmlConverterOptions options)
    {
        switch (property.Value.ValueKind)
        {
            case JsonValueKind.Null:
                return options.NoNullValues;

            case JsonValueKind.False:
                return options.NoFalseValues;

            case JsonValueKind.Number:
                if (options.NoZeroValues)
                {
                    if (property.Value.TryGetInt32(out int intValue))
                        return intValue == 0;
                    if (property.Value.TryGetInt64(out long longValue))
                        return longValue == 0;
                    if (property.Value.TryGetDouble(out double doubleValue))
                        return Math.Abs(doubleValue) < double.Epsilon;
                    if (property.Value.TryGetDecimal(
                        out decimal decimalValue))
                        return decimalValue == 0;
                }
                return false;

            default:
                return false;
        }
    }

}

/// <summary>
/// Options for <see cref="JsonToXmlConverter"/>.
/// </summary>
public class JsonToXmlConverterOptions : NamespaceOptions
{
    /// <summary>
    /// Gets or sets the names of the XML elements representing individual
    /// items in a JSON array. The key is the array property name, and the
    /// value is the name for each item element (e.g. key=<c>guys</c>,
    /// value=<c>guy</c>, essentially plural=singular). If no mapping is
    /// provided, the converter will try to singularize the array name
    /// automatically (e.g. "fragments" becomes "fragment"). If you need
    /// to set a namespace, add its prefix before colon, like <c>tei:div</c>.
    /// These prefixes are optionally defined in
    /// <see cref="NamespaceOptions.Namespaces"/>.
    /// </summary>
    public IDictionary<string, string>? WrappedEntryNames { get; set; }

    /// <summary>
    /// True to add an <c>n</c> attribute to array items with their
    /// ordinal number.
    /// </summary>
    public bool ArrayItemNumbering { get; set; }

    /// <summary>
    /// The name of the root element. Prefix is allowed, e.g. <c>tei:TEI</c>.
    /// Default is "root".
    /// </summary>
    public string RootElementName { get; set; } = "root";

    /// <summary>
    /// True to avoid generating elements for null-valued properties.
    /// </summary>
    public bool NoNullValues { get; set; }

    /// <summary>
    /// True to avoid generating elements for false boolean properties.
    /// </summary>
    public bool NoFalseValues { get; set; }

    /// <summary>
    /// True to avoid generating elements for zero numeric properties.
    /// </summary>
    public bool NoZeroValues { get; set; }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="JsonToXmlConverterOptions"/> class.
    /// </summary>
    public JsonToXmlConverterOptions()
    {
        DefaultNsPrefix = null;
    }
}
