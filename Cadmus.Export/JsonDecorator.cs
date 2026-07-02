using System;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cadmus.Export;

/// <summary>
/// JSON decorator helper.
/// </summary>
public static class JsonDecorator
{
    private static readonly JsonSerializerOptions _options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

    /// <summary>
    /// Decorates the layer part fragments by adding a key property
    /// (named after <paramref name="keyPropName"/>) to each fragment
    /// in the <c>fragments</c> array. The key is built from the part's
    /// type ID optionally followed by <c>:</c> and role ID, plus each
    /// fragment's index after <c>@</c>.
    /// </summary>
    /// <param name="json">The json.</param>
    /// <param name="keyPropName">Name of the key property.</param>
    /// <returns>Decorated JSON.</returns>
    /// <exception cref="ArgumentNullException">json or keyPropName
    /// </exception>
    public static string DecorateLayerPartFrr(string json,
        string keyPropName = "_key")
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(keyPropName);

        JsonNode? doc = JsonNode.Parse(json);
        JsonObject? root = doc?["root"]?.AsObject();
        if (root == null) return json;

        string? typeId = root["typeId"]?.GetValue<string>();
        if (typeId == null) return json;

        string? roleId = root["roleId"]?.GetValue<string>();
        string key = typeId;
        if (!string.IsNullOrEmpty(roleId)) key += ":" + roleId;

        JsonArray? frr = root["fragments"]?.AsArray();
        if (frr == null) return json;

        int i = 0;
        foreach (JsonNode? fr in frr)
        {
            fr![keyPropName] = $"{key}@{i}";
            i++;
        }

        return doc!.ToJsonString(_options);
    }
}
