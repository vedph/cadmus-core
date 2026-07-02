using Cadmus.Core.Config;
using System;
using System.IO;

namespace Cadmus.Import;

/// <summary>
/// JSON facet definition reader. This reads a JSON document containing either
/// an array of facet definitions, or a single facet definition.
/// </summary>
/// <seealso cref="IThesaurusReader" />
public sealed class JsonFacetReader : JsonArrayOrObjectReader<FacetDefinition>,
    IFacetReader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFacetReader"/> class.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <exception cref="ArgumentNullException">source</exception>
    public JsonFacetReader(Stream source) : base(source)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonFacetReader"/> class.
    /// </summary>
    /// <param name="json">The JSON code to read thesauri from.</param>
    /// <exception cref="ArgumentNullException">json</exception>
    public JsonFacetReader(string json) : base(json)
    {
    }
}
