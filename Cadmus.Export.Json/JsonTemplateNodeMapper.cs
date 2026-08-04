using Cadmus.Export.Mapping;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Fluid;

namespace Cadmus.Export.Json;

/// <summary>
/// JSON-based node mapper building a JSON document from a Fluid template
/// (https://github.com/sebastienros/fluid).
/// <para>Tag: <c>node-mapper.json.template</c>.</para>
/// </summary>
/// <remarks>This mapper is used to convert JSON code representing a source
/// Cadmus object into another JSON code with a different schema. Each mapping
/// contains a Fluid JSON template for its output. So, rather than having a
/// single, monolithic template for the whole object, we use node mappings
/// to apply many templates to different parts of the object. Finally, we
/// consolidate all the resulting results into a single JSON object
/// (<see cref="JsonObject"/>). This approach leverages the full selection
/// power of JMESPath (via mappings) and promotes a modular approach to the
/// transformation based on tree structures, similar to XSLT.
/// <para>The target JSON object collecting the output must be mutable while
/// mappings are being applied to it, so unlike the immutable
/// <see cref="JsonDocument"/> this mapper targets a <see cref="JsonObject"/>
/// (from <see cref="System.Text.Json.Nodes"/>), consistently with how e.g.
/// <c>JsonGraphNodeMapper</c> targets a mutable <c>GraphSet</c>. Callers
/// needing an immutable snapshot can get one via
/// <c>JsonDocument.Parse(target.ToJsonString())</c>.</para>
/// </remarks>
/// <seealso cref="JsonNodeMapper{TTarget}" />
[Tag("node-mapper.json.template")]
public sealed class JsonTemplateNodeMapper : JsonNodeMapper<JsonObject>
{
    private readonly FluidParser _parser;
    private readonly TemplateOptions _options;
    private readonly Dictionary<string, IFluidTemplate> _templates;

    /// <summary>
    /// Gets the Fluid filters available to the templates used by this
    /// mapper, preloaded with Fluid's standard filters, including its
    /// built-in <c>json</c> filter which serializes its input as JSON (e.g.
    /// <c>{{ value.name | json }}</c>); this is the recommended way to
    /// safely embed values into an output template, as Fluid does not
    /// encode its output for JSON by default. Add more filters here as
    /// required by your templates.
    /// </summary>
    public FilterCollection Filters => _options.Filters;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonTemplateNodeMapper"/>
    /// class.
    /// </summary>
    public JsonTemplateNodeMapper()
    {
        _parser = new FluidParser();
        _options = new TemplateOptions();
        _templates = [];
    }

    private string ResolvePlaceholder(PlaceholderNode node)
    {
        if (node.ChildrenCount == 0) return "";

        StringBuilder sb = new();
        foreach (PlaceholderNode child in node.Children
            .Where(child => child.Value != null))
        {
            sb.Append(child.Value);
        }

        string value = sb.ToString();
        return node.Type switch
        {
            PlaceholderNodeType.Metadatum => Data.TryGetValue(value,
                out object? d) ? d?.ToString() ?? "" : "",
            PlaceholderNodeType.Expression => ResolveDataExpression(value),
            PlaceholderNodeType.Macro => ResolveMacro(value),
            _ => ""
        };
    }

    /// <summary>
    /// Fill the specified template by resolving macros (<c>{!...}</c>),
    /// metadata placeholders (<c>{$...}</c>), or data expression
    /// placeholders (<c>{@...}</c>) in the specified template. This is used
    /// to resolve <see cref="NodeMapping.Sid"/> and
    /// <see cref="JsonNodeMapping.TargetProperty"/> templates; the JSON
    /// output of a mapping is rendered via Fluid instead (see
    /// <see cref="BuildOutput"/>).
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="filter">Not used.</param>
    protected override string ResolveTemplate(string template, bool filter)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (template.Length == 0) return template;

        return PlaceholderTree.Create(template).Resolve(ResolvePlaceholder);
    }

    /// <summary>
    /// Gets the JSON value currently selected by the mapping being applied,
    /// converted into plain CLR objects (dictionaries, lists, strings,
    /// numbers, booleans) so that Fluid can bind to it.
    /// </summary>
    private object? GetCurrentValue()
    {
        // the base class sets Data["."] only for a scalar leaf value: in
        // that case, just reuse it as-is rather than round-tripping it
        // through JSON parsing (which would lose e.g. a string looking
        // like a number).
        if (Data.TryGetValue(".", out object? scalar)) return scalar;

        string raw = ResolveDataExpression(".");
        if (string.IsNullOrEmpty(raw)) return null;

        using JsonDocument doc = JsonDocument.Parse(raw);
        return ConvertElement(doc.RootElement);
    }

    private static object? ConvertElement(JsonElement element) =>
        element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(
                p => p.Name, p => ConvertElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertElement).ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out long l)
                ? l : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };

    private string RenderOutput(string? sid, JsonNodeMapping mapping)
    {
        if (!_templates.TryGetValue(mapping.Output!, out IFluidTemplate?
            template))
        {
            if (!_parser.TryParse(mapping.Output!, out template,
                out string error))
            {
                throw new InvalidOperationException(
                    $"Invalid Fluid template in mapping {mapping}: {error}");
            }
            _templates[mapping.Output!] = template;
        }

        TemplateContext context = new(_options);
        context.SetValue("value", GetCurrentValue()!);
        context.SetValue("metadata", Data);
        context.SetValue("sid", sid ?? "");

        return template.Render(context);
    }

    /// <summary>
    /// Deep-merges <paramref name="source"/> into <paramref name="target"/>:
    /// object properties are merged recursively (existing properties are
    /// enriched or overridden by <paramref name="source"/>'s), while arrays
    /// found under the same property in both objects get concatenated.
    /// </summary>
    private static void MergeObject(JsonObject target, JsonObject source)
    {
        foreach (KeyValuePair<string, JsonNode?> prop in source)
        {
            if (prop.Value is JsonObject sourceChild &&
                target[prop.Key] is JsonObject targetChild)
            {
                MergeObject(targetChild, sourceChild);
            }
            else if (prop.Value is JsonArray sourceArray &&
                target[prop.Key] is JsonArray targetArray)
            {
                foreach (JsonNode? item in sourceArray)
                    targetArray.Add(item?.DeepClone());
            }
            else
            {
                target[prop.Key] = prop.Value?.DeepClone();
            }
        }
    }

    private static void SetOrMerge(JsonObject target, string? targetProperty,
        JsonNode value)
    {
        if (string.IsNullOrEmpty(targetProperty))
        {
            // without a target property, the output can only be merged at
            // the root, and thus must itself be an object
            if (value is JsonObject valueObject)
                MergeObject(target, valueObject);
            else
            {
                throw new InvalidOperationException(
                    "A mapping without a TargetProperty must output a " +
                    $"JSON object, but its output was: {value.ToJsonString()}");
            }
            return;
        }

        if (target[targetProperty] is JsonObject existingObject &&
            value is JsonObject newObject)
        {
            MergeObject(existingObject, newObject);
        }
        else if (target[targetProperty] is JsonArray existingArray &&
            value is JsonArray newArray)
        {
            foreach (JsonNode? item in newArray)
                existingArray.Add(item?.DeepClone());
        }
        else
        {
            target[targetProperty] = value.DeepClone();
        }
    }

    /// <summary>
    /// Builds the output for the specified matched mapping, using the
    /// specified SID if any, into <paramref name="target"/>: this renders
    /// the mapping's Fluid <see cref="JsonNodeMapping.Output"/> template
    /// and merges the resulting JSON into <paramref name="target"/>, under
    /// <see cref="JsonNodeMapping.TargetProperty"/> if any, or at its root
    /// otherwise.
    /// </summary>
    /// <param name="sid">The SID resolved for this mapping (which might be
    /// inherited from an ancestor mapping), or null/empty when none could
    /// be resolved.</param>
    /// <param name="mapping">The matched mapping.</param>
    /// <param name="target">The target object collecting the output.</param>
    protected override void BuildOutput(string? sid, NodeMapping mapping,
        JsonObject target)
    {
        JsonNodeMapping jm = (JsonNodeMapping)mapping;
        if (string.IsNullOrEmpty(jm.Output)) return;

        string rendered = RenderOutput(sid, jm);
        if (string.IsNullOrWhiteSpace(rendered)) return;

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(rendered);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Invalid JSON output from mapping {mapping}: {rendered}",
                ex);
        }
        if (node == null) return;

        string? targetProperty = string.IsNullOrEmpty(jm.TargetProperty)
            ? null
            : ResolveTemplate(jm.TargetProperty, false);

        SetOrMerge(target, targetProperty, node);
    }
}
