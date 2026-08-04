using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadmus.Export.Mapping;

/// <summary>
/// JSON converter for the abstract <see cref="NodeMapping"/> type, bound to
/// a specific concrete derived type <typeparamref name="T"/>. This is
/// required because <see cref="System.Text.Json"/> cannot (de)serialize an
/// abstract type on its own (e.g. when reading a mapping's
/// <see cref="NodeMapping.Children"/>).
/// <para>
/// As this library (Cadmus.Export) is agnostic of any specific output (e.g.
/// the RDF graph output implemented by a <c>GraphNodeMapping</c> defined in
/// Cadmus.Graph), it cannot hardcode a single concrete type here. Instead,
/// each consumer using a specific <see cref="NodeMapping"/>-derived type
/// should register an instance of this converter closed over that type into
/// the <see cref="JsonSerializerOptions.Converters"/> used for (de)serializing
/// mappings, e.g. <c>options.Converters.Add(new
/// NodeMappingJsonConverter&lt;GraphNodeMapping&gt;());</c>.
/// </para>
/// </summary>
/// <typeparam name="T">The concrete <see cref="NodeMapping"/> derived type
/// to use when reading and writing mappings.</typeparam>
public sealed class NodeMappingJsonConverter<T> : JsonConverter<NodeMapping>
    where T : NodeMapping
{
    /// <summary>
    /// Reads and converts the JSON to a <typeparamref name="T"/>.
    /// </summary>
    public override NodeMapping? Read(ref Utf8JsonReader reader,
        Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<T>(ref reader, options);
    }

    /// <summary>
    /// Writes the specified <see cref="NodeMapping"/> (a
    /// <typeparamref name="T"/>) as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, NodeMapping value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (T)value, options);
    }
}
