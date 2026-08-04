using System;
using System.Collections.Generic;
using System.Text.Json;
using DevLab.JmesPath;
using Microsoft.Extensions.Logging;

namespace Cadmus.Export.Mapping;

/// <summary>
/// Base class for JSON-based node mappers. This class implements the
/// select-and-process logic shared by any mapper transforming a JSON source
/// object via a tree of <see cref="NodeMapping"/>'s selected through JMES
/// path expressions, regardless of the specific output the mapper builds
/// for each processed node (e.g. RDF nodes/triples, or another JSON object).
/// Derive from this class and implement <see cref="BuildOutput"/> to define
/// what output to build for a matched mapping.
/// </summary>
/// <typeparam name="TTarget">The type of the object collecting the output
/// generated while applying the mappings.</typeparam>
/// <seealso cref="NodeMapper" />
public abstract class JsonNodeMapper<TTarget> : NodeMapper
{
    private readonly JmesPath _jmes;
    private JsonDocument? _doc;
    private string? _lastSidSource;

    /// <summary>
    /// Gets the source type of the mapping tree currently being applied.
    /// This is set once for all the descendants when <see cref="Map"/>
    /// is invoked.
    /// </summary>
    protected int SourceType { get; private set; }

    /// <summary>
    /// Gets or sets the last SID resolved while applying the current
    /// mapping tree. This is inherited by descendant mappings which do not
    /// override it with their own <see cref="NodeMapping.Sid"/>.
    /// </summary>
    protected string? LastSid { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonNodeMapper{TTarget}"/>
    /// class.
    /// </summary>
    protected JsonNodeMapper()
    {
        _jmes = new();
    }

    /// <summary>
    /// Resolves the specified data expression into a string.
    /// </summary>
    /// <param name="expression">The data expression.</param>
    /// <returns>The resolved string.</returns>
    protected override string ResolveDataExpression(string expression)
    {
        if (_doc == null) return "";

        // corner case: "." just means current value.
        // Also, any value kind not being an object or an array is just
        // a primitive, so we end up returning the current value.
        if (expression == "." ||
            (_doc.RootElement.ValueKind != JsonValueKind.Object
            && _doc.RootElement.ValueKind != JsonValueKind.Array))
        {
            return _doc.RootElement.ToString();
        }

        // an array might include a single primitive, so return it
        if (_doc.RootElement.ValueKind == JsonValueKind.Array
            && _doc.RootElement.GetArrayLength() == 1
            && _doc.RootElement[0].ValueKind != JsonValueKind.Object
            && _doc.RootElement[0].ValueKind != JsonValueKind.Array)
        {
            return _doc.RootElement[0].ToString();
        }

        // else evaluate expression from current object/array
        string json = JsonSerializer.Serialize(_doc.RootElement);
        string? result = _jmes.Transform(json, expression);
        if (string.IsNullOrEmpty(result)) return "";

        return JsonDocument.Parse(result).RootElement.ToString() ?? "";
    }

    /// <summary>
    /// Builds the output for the specified matched mapping, using the
    /// specified SID if any, into <paramref name="target"/>.
    /// </summary>
    /// <param name="sid">The SID resolved for this mapping (which might be
    /// inherited from an ancestor mapping), or null/empty when none could
    /// be resolved.</param>
    /// <param name="mapping">The matched mapping.</param>
    /// <param name="target">The target object collecting the output.</param>
    protected abstract void BuildOutput(string? sid, NodeMapping mapping,
        TTarget target);

    /// <summary>
    /// Resolves the specified template (e.g. a <see cref="NodeMapping.Sid"/>
    /// template) by filling in its placeholders (macros, metadata, data
    /// expressions, and any output-specific placeholder, like a reference to
    /// a graph node for a RDF target) with their values. Derived classes
    /// implement this according to the placeholder syntax and context
    /// relevant to their specific output.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="uidFilter">True to apply a UID-safe filter to the
    /// resolved result before returning it.</param>
    /// <returns>The resolved template.</returns>
    protected abstract string ResolveTemplate(string template, bool uidFilter);

    private void ApplyMapping(string? sid, string json, NodeMapping mapping,
        TTarget target, int itemIndex = -1)
    {
        Logger?.LogDebug("Mapping {mapping}", mapping);

        if (IsMappingTracingEnabled)
        {
            IList<NodeMapping>? traced = null;
            if (Data.TryGetValue(APPLIED_MAPPING_LIST, out object? mappings))
                traced = mappings as IList<NodeMapping>;
            if (traced == null)
            {
                traced = [];
                Data[APPLIED_MAPPING_LIST] = traced;
            }
            // avoid sequences of duplicates (which happen in arrays)
            if (traced.Count == 0 || traced[^1] != mapping)
                traced.Add(mapping);
        }

        // if we're dealing with an array's item, we do not want to compute
        // the mapping's expression, but just use the received json
        // representing the item itself.
        string? result;
        if (itemIndex == -1)
        {
            try
            {
                result = mapping.Source == "."
                    ? json
                    : _jmes.Transform(json, mapping.Source);
                // provide index anyway, because some mappings might need it,
                // even if effectively used only for array items later
                Data["index"] = -1;
            }
            catch (Exception ex)
            {
                throw new AggregateException(
                    $"Eval error \"{ex.Message}\" at mapping {mapping}", ex);
            }
        }
        else result = json;

        // scalar pattern filter
        if (!mapping.IsScalarMatch(result)) return;

        // get the result into the current document
        _doc = JsonDocument.Parse(result);

        // generate SID if specified in mapping
        if (mapping.Sid != null)
        {
            sid = ResolveTemplate(mapping.Sid!, false);
            if (!string.IsNullOrEmpty(sid))
            {
                LastSid = sid;
                _lastSidSource = mapping.Sid;
            }
        }
        else if (itemIndex > -1 && _lastSidSource?.IndexOf(
            "$index", StringComparison.Ordinal) > -1)
        {
            // corner case: inherited SID with an array item: resolve it
            // again if including $index
            sid = ResolveTemplate(mapping.Sid!, false);
            if (!string.IsNullOrEmpty(sid)) LastSid = sid;
        }

        // process document with result, according to its root type
        switch (_doc.RootElement.ValueKind)
        {
            // null or undefined does not trigger output
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                break;
            case JsonValueKind.Object:
                if (string.IsNullOrEmpty(sid)) sid = LastSid;
                BuildOutput(sid, mapping, target);
                break;
            // an array does not trigger output, but applies its mapping
            // to each of its items
            case JsonValueKind.Array:
                // for each array's item, apply mapping and its children
                int index = 0;
                foreach (JsonElement item in _doc.RootElement.EnumerateArray())
                {
                    Data["index"] = index;
                    ApplyMapping(sid, item.GetRawText(),
                        mapping, target, index++);
                }
                Data.Remove("index");
                break;
            // else it's a terminal, build output
            default:
                // set current leaf variable
                Data["."] = _doc.RootElement.ToString();
                if (string.IsNullOrEmpty(sid)) sid = LastSid;
                BuildOutput(sid, mapping, target);
                break;
        }
        _doc = null;

        // process this mapping's children recursively
        if (mapping.HasChildren)
        {
            foreach (NodeMapping child in mapping.Children!)
                ApplyMapping(sid, result, child, target);
        }
    }

    /// <summary>
    /// Map the specified source into the <paramref name="target"/> object.
    /// </summary>
    /// <param name="source">The source object, here a JSON string.</param>
    /// <param name="mapping">The mapping to apply.</param>
    /// <param name="target">The target object collecting the output.</param>
    /// <exception cref="ArgumentNullException">mapping or target</exception>
    public virtual void Map(object source, NodeMapping mapping, TTarget target)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(target);

        // reset state
        SourceType = 0;
        LastSid = null;
        _doc = null;

        // source is JSON
        string? json = source as string;
        if (string.IsNullOrEmpty(json)) return;

        // set source type once for all the descendants
        SourceType = mapping.SourceType;

        // apply mapping recursively
        ApplyMapping(null, json, mapping, target);
    }
}
