using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cadmus.Graph;

/// <summary>
/// JSON converter for the abstract <see cref="NodeMapping"/> type. This is
/// required because <see cref="System.Text.Json"/> cannot deserialize an
/// abstract type on its own (e.g. when reading a mapping's
/// <see cref="NodeMapping.Children"/>). As <see cref="GraphNodeMapping"/> is
/// currently the only concrete <see cref="NodeMapping"/> used in this
/// library, this converter reads and writes any <see cref="NodeMapping"/>
/// as a <see cref="GraphNodeMapping"/>.
/// </summary>
public sealed class NodeMappingJsonConverter : JsonConverter<NodeMapping>
{
    /// <summary>
    /// Reads and converts the JSON to a <see cref="GraphNodeMapping"/>.
    /// </summary>
    public override NodeMapping? Read(ref Utf8JsonReader reader,
        Type typeToConvert, JsonSerializerOptions options)
    {
        return JsonSerializer.Deserialize<GraphNodeMapping>(ref reader, options);
    }

    /// <summary>
    /// Writes the specified <see cref="NodeMapping"/> (a
    /// <see cref="GraphNodeMapping"/>) as JSON.
    /// </summary>
    public override void Write(Utf8JsonWriter writer, NodeMapping value,
        JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, (GraphNodeMapping)value, options);
    }
}
