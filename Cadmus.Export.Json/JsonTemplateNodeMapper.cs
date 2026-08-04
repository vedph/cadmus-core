using Cadmus.Export.Mapping;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
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
/// (<see cref="JsonDocument"/>). This approach leverages the full selection
/// power of JMESPath (via mappings) and promotes a modular approach to the
/// transformation based on tree structures, similar to XSLT.</remarks>
/// <seealso cref="JsonNodeMapper{TTarget}" />
[Tag("node-mapper.json.template")]
public sealed class JsonTemplateNodeMapper : JsonNodeMapper<JsonDocument>
{
    // TODO add public Filters get-only property to register Fluid filters
    // for the Fluid templates used in this mapper

    /// <summary>
    /// Fill the specified template by resolving macros (<c>!{...}</c>),
    /// or metadata placeholders (<c>${...}</c>) in the specified template.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="filter">Not used.</param>
    protected override string ResolveTemplate(string template, bool filter)
    {
        // TODO: implement for resolving macros (<c>!{...}</c>) or metadata
        // (<c>${...}</c>) placeholders in the template
        return template;
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
    protected override void BuildOutput(string? sid, NodeMapping mapping,
        JsonDocument target)
    {
        // TODO: implement
        throw new NotImplementedException();
    }
}
