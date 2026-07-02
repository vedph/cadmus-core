using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Cadmus.Export.Rdf;

/// <summary>
/// RDF/XML format RDF writer.
/// </summary>
public sealed class XmlRdfWriter : RdfWriter
{
    internal static readonly XNamespace RDF_NS =
        "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    internal static readonly XNamespace XML_NS =
        "http://www.w3.org/XML/1998/namespace";

    private XDocument? _document;

    /// <summary>
    /// Creates a new RDF/XML writer.
    /// </summary>
    /// <param name="settings">The RDF export settings.</param>
    /// <param name="prefixMappings">The prefix mappings.</param>
    /// <param name="uriMappings">The URI mappings.</param>
    public XmlRdfWriter(RdfExportSettings settings,
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
        XElement rootElement = new(RDF_NS + "RDF");

        // add namespace declarations
        rootElement.SetAttributeValue(XNamespace.Xmlns + "rdf",
            RDF_NS.NamespaceName);

        foreach (KeyValuePair<string, string> mapping in _prefixMappings)
        {
            if (mapping.Key != "rdf") // rdf is already declared
            {
                rootElement.SetAttributeValue(XNamespace.Xmlns + mapping.Key,
                    mapping.Value);
            }
        }

        // add base URI if specified
        if (!string.IsNullOrEmpty(_settings.BaseUri))
            rootElement.SetAttributeValue(XML_NS + "base", _settings.BaseUri);

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
    /// Writes the given triples in RDF/XML format.
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

            // create Description element
            XElement descriptionElement = new(RDF_NS + "Description",
                new XAttribute(RDF_NS + "about", subjectUri));

            // add predicate elements for this subject
            foreach (RdfTriple triple in subjectGroup)
            {
                XElement predicateElement = CreatePredicateElement(triple);
                descriptionElement.Add(predicateElement);
            }

            // add to document root
            _document.Root.Add(descriptionElement);
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
        string predicateUri = GetUriForId(triple.PredicateId);
        XName predicateName = CreateXName(predicateUri);

        if (!string.IsNullOrEmpty(triple.ObjectLiteral))
        {
            // literal value
            XElement literalElement = new(predicateName, triple.ObjectLiteral);

            if (!string.IsNullOrEmpty(triple.ObjectLiteralLanguage))
            {
                literalElement.SetAttributeValue(XML_NS + "lang",
                    triple.ObjectLiteralLanguage);
            }
            else if (!string.IsNullOrEmpty(triple.ObjectLiteralType))
            {
                string datatype = triple.ObjectLiteralType.Contains(':') &&
                    !triple.ObjectLiteralType.StartsWith("http")
                    ? UriHelper.ExpandUri(triple.ObjectLiteralType, _prefixMappings)
                    : triple.ObjectLiteralType;
                literalElement.SetAttributeValue(RDF_NS + "datatype", datatype);
            }

            return literalElement;
        }
        else if (triple.ObjectId.HasValue)
        {
            // resource reference
            string objectUri = GetFullUri(triple.ObjectId.Value);
            return new XElement(predicateName,
                new XAttribute(RDF_NS + "resource", objectUri));
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

        // fallback: use full URI as local name with a default namespace
        // this creates a valid RDF/XML element but with a generated namespace
        string fallbackNamespace = "http://unknown.namespace/";
        string fallbackLocalName = $"predicate_{GetPredicateIdFromUri(uri)}";

        // add the fallback namespace to the document if not already present
        if (_document?.Root != null)
        {
            string fallbackPrefix = "ns" + GetPredicateIdFromUri(uri);
            if (!_prefixMappings.ContainsKey(fallbackPrefix))
            {
                _document.Root.SetAttributeValue(XNamespace.Xmlns + fallbackPrefix,
                    fallbackNamespace);
            }
        }

        return XName.Get(fallbackLocalName, fallbackNamespace);
    }

    /// <summary>
    /// Checks if a string is a valid XML name.
    /// </summary>
    /// <param name="name">The name to check.</param>
    /// <returns>True if valid XML name.</returns>
    private static bool IsValidXmlName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        // basic check: must start with letter or underscore
        if (!char.IsLetter(name[0]) && name[0] != '_')
            return false;

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
    /// Extracts predicate ID from URI for fallback naming.
    /// </summary>
    /// <param name="uri">The URI.</param>
    /// <returns>A safe identifier.</returns>
    private static string GetPredicateIdFromUri(string uri)
    {
        // simple heuristic: take last part after / or #
        int lastSlash = uri.LastIndexOf('/');
        int lastHash = uri.LastIndexOf('#');
        int start = Math.Max(lastSlash, lastHash);

        if (start >= 0 && start < uri.Length - 1)
        {
            string candidate = uri[(start + 1)..];
            if (IsValidXmlName(candidate))
                return candidate;
        }

        return uri.GetHashCode().ToString("X");
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
