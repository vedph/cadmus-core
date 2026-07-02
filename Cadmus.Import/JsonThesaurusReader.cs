using Cadmus.Core.Config;
using System;
using System.IO;

namespace Cadmus.Import;

/// <summary>
/// JSON thesaurus reader. This reads a JSON document containing either an
/// array of thesauri, or a single thesaurus.
/// </summary>
/// <seealso cref="IThesaurusReader" />
public sealed class JsonThesaurusReader : JsonArrayOrObjectReader<Thesaurus>,
    IThesaurusReader
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonThesaurusReader"/> class.
    /// </summary>
    /// <param name="source">The source stream.</param>
    /// <exception cref="ArgumentNullException">source</exception>
    public JsonThesaurusReader(Stream source) : base(source)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonThesaurusReader"/> class.
    /// </summary>
    /// <param name="json">The JSON code to read thesauri from.</param>
    /// <exception cref="ArgumentNullException">json</exception>
    public JsonThesaurusReader(string json) : base(json)
    {
    }
}
