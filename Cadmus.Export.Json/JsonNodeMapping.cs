using Cadmus.Export.Mapping;
using System.Text;

namespace Cadmus.Export.Json;

/// <summary>
/// A node mapping used to build JSON from a source JSON object, used by
/// <see cref="JsonTemplateNodeMapper"/>.
/// </summary>
public sealed class JsonNodeMapping : NodeMapping
{
    /// <summary>
    /// The Fluid template (https://github.com/sebastienros/fluid) used to
    /// render the JSON fragment output by this mapping when it matches its
    /// <see cref="NodeMapping.Source"/>. The template is evaluated with:
    /// <list type="bullet">
    /// <item><c>value</c>: the JSON value currently selected by this
    /// mapping (an object exposes its properties, an array its items, a
    /// scalar is exposed as a plain string);</item>
    /// <item><c>metadata</c>: the mapper's metadata dictionary;</item>
    /// <item><c>sid</c>: the SID resolved for this mapping, if any.</item>
    /// </list>
    /// The template's result must be valid JSON. As Fluid does not encode
    /// its output by default, use the mapper's built-in <c>json</c> filter
    /// (e.g. <c>{{ value.name | json }}</c>) to safely embed arbitrary
    /// values into it.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// The name of the property of the root JSON object under which the
    /// output of this mapping is to be merged. This can contain the same
    /// placeholders used by <see cref="NodeMapping.Sid"/> (metadata,
    /// data expressions, macros). When null or empty, the output (which
    /// must then be a JSON object) is merged directly into the root JSON
    /// object. Multiple mappings can target the same property: when they
    /// both output objects they get deep-merged (existing properties
    /// enriched or overridden), when they both output arrays the new
    /// items get appended, otherwise the newer output replaces the older.
    /// </summary>
    public string? TargetProperty { get; set; }

    /// <summary>
    /// Clones this instance, deep copying all the <see cref="NodeMapping"/>'s.
    /// </summary>
    /// <returns>New mapping object.</returns>
    public override JsonNodeMapping Clone()
    {
        JsonNodeMapping clone = new()
        {
            Output = Output,
            TargetProperty = TargetProperty
        };
        CopyTo(clone);
        return clone;
    }

    /// <summary>
    /// Appends the output summary to the string representation built by
    /// <see cref="NodeMapping.ToString"/>.
    /// </summary>
    /// <param name="sb">The target string builder.</param>
    protected override void AppendExtra(StringBuilder sb)
    {
        if (!string.IsNullOrEmpty(TargetProperty))
            sb.Append(" => ").Append(TargetProperty);
        if (Output != null) sb.Append(" -> ").Append(Output);
    }
}
