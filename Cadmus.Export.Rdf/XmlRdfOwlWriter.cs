using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Cadmus.Export.Rdf;

/// <summary>
/// RDF/XML OWL format RDF writer. This writer outputs RDF in OWL format,
/// using owl:NamedIndividual elements instead of rdf:Description.
/// </summary>
/// <remarks>In this variant of <see cref="XmlRdfWriter"/> the RDF/XML output
/// is modified to use OWL-specific elements and attributes:
/// <para>- Uses &lt;owl:NamedIndividual rdf:about="..."&gt; as the container
/// element.
/// </para>
/// <para>- Type is still expressed as &lt;rdf:type rdf:resource="..."&gt;
/// but nested inside.</para>
/// <para>- Properties follow the same pattern (like &lt;rdfs:comment&gt;).
/// </para>
/// <para>- Otherwise maintains the same RDF/XML structure.</para>
/// </remarks>
public sealed class XmlRdfOwlWriter : RdfWriter
{
    private static readonly XNamespace OWL_NS =
        "http://www.w3.org/2002/07/owl#";

    private XDocument? _document;

    /// <summary>
    /// Creates a new RDF/XML OWL writer.
    /// </summary>
    /// <param name="settings">The RDF export settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    public XmlRdfOwlWriter(RdfExportSettings settings,
        Dictionary<string, string> prefixMappings,
        Dictionary<int, string> uriMappings)
        : base(settings, prefixMappings, uriMappings)
    {
    }

    /// <summary>
    /// Write the header, including comments and namespace declarations.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public override Task WriteHeaderAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // create the root RDF element with all namespace declarations
        XElement rootElement = new(XmlRdfWriter.RDF_NS + "RDF");

        // add namespace declarations
        rootElement.SetAttributeValue(XNamespace.Xmlns + "rdf",
            XmlRdfWriter.RDF_NS.NamespaceName);
        rootElement.SetAttributeValue(XNamespace.Xmlns + "owl",
            OWL_NS.NamespaceName);

        foreach (KeyValuePair<string, string> mapping in _prefixMappings)
        {
            if (mapping.Key != "rdf" && mapping.Key != "owl") // already declared
            {
                rootElement.SetAttributeValue(XNamespace.Xmlns + mapping.Key,
                    mapping.Value);
            }
        }

        // add base URI if specified
        if (!string.IsNullOrEmpty(_settings.BaseUri))
        {
            rootElement.SetAttributeValue(XmlRdfWriter.XML_NS + "base",
                _settings.BaseUri);
        }

        // create the document with declaration using the configured encoding
        _document = new XDocument(
            new XDeclaration("1.0", _settings.Encoding.WebName, null),
            rootElement);

        // add comments if enabled
        if (_settings.IncludeComments)
        {
            _document.Root!.AddFirst(
                new XComment(" RDF data exported from Cadmus Graph database "),
                new XComment($" Export date: {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ} "));
        }

        // all content will be written at once in WriteFooterAsync
        return Task.CompletedTask;
    }

    /// <summary>
    /// Writes the given triples in RDF/XML OWL format.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="triples">The triples.</param>
    /// <exception cref="ArgumentNullException">writer or triples</exception>
    public override Task WriteAsync(TextWriter writer, List<RdfTriple> triples)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(triples);

        if (_document?.Root == null)
            throw new InvalidOperationException(
                "WriteHeaderAsync must be called before WriteAsync");

        // group triples by subject for better RDF/XML structure
        IEnumerable<IGrouping<int, RdfTriple>> groupedTriples =
            triples.GroupBy(t => t.SubjectId);

        foreach (IGrouping<int, RdfTriple> subjectGroup in groupedTriples)
        {
            string subjectUri = GetFullUri(subjectGroup.Key);

            // create NamedIndividual element (OWL format)
            XElement namedIndividualElement = new(OWL_NS + "NamedIndividual",
                new XAttribute(XmlRdfWriter.RDF_NS + "about", subjectUri));

            // add predicate elements for this subject
            foreach (RdfTriple triple in subjectGroup)
            {
                XElement predicateElement = CreatePredicateElement(triple);
                namedIndividualElement.Add(predicateElement);
            }

            // add to document root
            _document.Root.Add(namedIndividualElement);
        }

        // all content will be written at once in WriteFooterAsync
        return Task.CompletedTask;
    }

    /// <summary>
    /// Creates a predicate element for the given triple.
    /// </summary>
    /// <param name="triple">The triple.</param>
    /// <returns>The predicate element.</returns>
    private XElement CreatePredicateElement(RdfTriple triple)
    {
        string predicateUri = GetFullUri(triple.PredicateId);
        XName predicateName = CreateXName(predicateUri);

        if (!string.IsNullOrEmpty(triple.ObjectLiteral))
        {
            // literal value
            XElement literalElement = new(predicateName, triple.ObjectLiteral);

            if (!string.IsNullOrEmpty(triple.ObjectLiteralLanguage))
            {
                literalElement.SetAttributeValue(XmlRdfWriter.XML_NS + "lang",
                    triple.ObjectLiteralLanguage);
            }
            else if (!string.IsNullOrEmpty(triple.ObjectLiteralType))
            {
                string datatype = triple.ObjectLiteralType.Contains(':') &&
                    !triple.ObjectLiteralType.StartsWith("http")
                    ? UriHelper.ExpandUri(triple.ObjectLiteralType, _prefixMappings)
                    : triple.ObjectLiteralType;
                literalElement.SetAttributeValue(XmlRdfWriter.RDF_NS +
                    "datatype", datatype);
            }

            return literalElement;
        }
        else if (triple.ObjectId.HasValue)
        {
            // resource reference
            string objectUri = GetFullUri(triple.ObjectId.Value);
            return new XElement(predicateName,
                new XAttribute(XmlRdfWriter.RDF_NS + "resource", objectUri));
        }
        else
        {
            // empty element (shouldn't happen in valid RDF, but handle gracefully)
            return new XElement(predicateName);
        }
    }

    /// <summary>
    /// Creates an XName from a URI, using prefixed form when possible.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The XName.</returns>
    private XName CreateXName(string uri)
    {
        // if it's already in prefixed form and not a full URI, use it directly
        if (uri.Contains(':') && !uri.StartsWith("http"))
        {
            string[] parts = uri.Split(':', 2);
            if (parts.Length == 2 && _prefixMappings.TryGetValue(parts[0],
                out string? namespaceUri))
            {
                return XName.Get(parts[1], namespaceUri);
            }
        }

        // try to find a matching namespace
        foreach (KeyValuePair<string, string> mapping in _prefixMappings)
        {
            if (uri.StartsWith(mapping.Value))
            {
                string localName = uri[mapping.Value.Length..];
                // ensure the local name is a valid XML name
                if (IsValidXmlName(localName))
                {
                    return XName.Get(localName, mapping.Value);
                }
            }
        }

        // if no prefix match found, we need to split the URI properly
        // try to extract namespace and local name from the full URI
        int splitIndex = GetUriSplitIndex(uri);
        if (splitIndex > 0 && splitIndex < uri.Length)
        {
            string namespaceUri = uri[..splitIndex];
            string localName = uri[splitIndex..];

            if (IsValidXmlName(localName))
            {
                // add this namespace to the document if not already present
                if (_document?.Root != null)
                {
                    // generate a prefix for this namespace
                    string prefix = GeneratePrefixForNamespace(namespaceUri);
                    if (!_prefixMappings.ContainsValue(namespaceUri))
                    {
                        _document.Root.SetAttributeValue(XNamespace.Xmlns + prefix,
                            namespaceUri);
                        _prefixMappings[prefix] = namespaceUri;
                    }
                }

                return XName.Get(localName, namespaceUri);
            }
        }

        // ultimate fallback: create a safe element name
        throw new InvalidOperationException(
            $"Cannot create valid XML name from URI: {uri}. " +
            "Ensure all predicates have proper namespace mappings configured.");
    }

    /// <summary>
    /// Finds the best split point in a URI to separate namespace from local name.
    /// </summary>
    /// <param name="uri">The URI to split.</param>
    /// <returns>The index where to split, or -1 if no good split point found.
    /// </returns>
    private static int GetUriSplitIndex(string uri)
    {
        // prefer splitting after # (fragment identifier)
        int hashIndex = uri.LastIndexOf('#');
        if (hashIndex >= 0 && hashIndex < uri.Length - 1)
            return hashIndex + 1;

        // otherwise split after last / (path separator)
        int slashIndex = uri.LastIndexOf('/');
        if (slashIndex >= 0 && slashIndex < uri.Length - 1)
            return slashIndex + 1;

        return -1;
    }

    /// <summary>
    /// Generates a unique prefix for a namespace.
    /// </summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <returns>A unique prefix.</returns>
    private string GeneratePrefixForNamespace(string namespaceUri)
    {
        // check if this namespace already has a prefix
        foreach (KeyValuePair<string, string> mapping in _prefixMappings)
        {
            if (mapping.Value == namespaceUri)
                return mapping.Key;
        }

        // generate a new prefix
        int counter = 1;
        string prefix;
        do
        {
            prefix = $"ns{counter}";
            counter++;
        } while (_prefixMappings.ContainsKey(prefix));

        return prefix;
    }

    /// <summary>
    /// Checks if a string is a valid XML name.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True if valid XML name.</returns>
    private static bool IsValidXmlName(string name)
    {
        if (string.IsNullOrEmpty(name)) return false;

        // basic check: must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;

        // check remaining characters
        for (int i = 1; i < name.Length; i++)
        {
            char c = name[i];
            if (!char.IsLetterOrDigit(c) && c != '-' && c != '_' && c != '.')
                return false;
        }

        return true;
    }

    /// <summary>
    /// Write the footer.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public override async Task WriteFooterAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (_document == null)
            throw new InvalidOperationException(
                "WriteHeaderAsync must be called before WriteFooterAsync");

        // determine save options based on pretty print setting
        SaveOptions saveOptions = _settings.PrettyPrint
            ? SaveOptions.OmitDuplicateNamespaces
            : SaveOptions.OmitDuplicateNamespaces | SaveOptions.DisableFormatting;

        // use a custom StringWriter that respects the encoding from settings
        using var stringWriter = new EncodingStringWriter(_settings.Encoding);
        _document.Save(stringWriter, saveOptions);

        string xmlContent = stringWriter.ToString();
        await writer.WriteAsync(xmlContent);
    }

    /// <summary>
    /// Custom StringWriter that reports the correct encoding.
    /// </summary>
    private class EncodingStringWriter(Encoding encoding) : StringWriter
    {
        private readonly Encoding _encoding = encoding;

        public override Encoding Encoding => _encoding;
    }
}
