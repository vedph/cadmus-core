using System.IO;
using System.Text.Json;

namespace Cadmus.Import;

/// <summary>
/// Provides functionality to read application settings from a JSON source as
/// either an array or an object.
/// </summary>
/// <remarks>Use this class to parse and access configuration data stored in
/// JSON format from a stream or a string. The settings can be represented as
/// either a JSON array or a JSON object, allowing flexibility in the
/// structure of the configuration data.</remarks>
public sealed class JsonSettingsReader : JsonArrayOrObjectReader<JsonElement>
{
    /// <summary>
    /// Initializes a new instance of the JsonSettingsReader class using the
    /// specified data stream as the source of JSON
    /// settings.
    /// </summary>
    /// <param name="source">The stream containing the JSON data to be read.
    /// The stream must be readable and positioned at the beginning of
    /// the JSON content.</param>
    public JsonSettingsReader(Stream source) : base(source)
    {
    }

    /// <summary>
    /// Initializes a new instance of the JsonSettingsReader class using the
    /// specified JSON string as the settings
    /// source.
    /// </summary>
    /// <param name="json">A string containing the JSON data to be used for
    /// reading settings. Cannot be null or empty.</param>
    public JsonSettingsReader(string json) : base(json)
    {
    }
}
