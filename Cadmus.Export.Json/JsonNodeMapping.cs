using Cadmus.Export.Mapping;
using System;
using System.Collections.Generic;
using System.Text;

namespace Cadmus.Export.Json;

/// <summary>
/// A node mapping used to build JSON from a source JSON object, used by
/// <see cref="JsonTemplateNodeMapper"/>.
/// </summary>
public sealed class JsonNodeMapping : NodeMapping
{
    // TODO: add Output property for the Fluid template output

    public override NodeMapping Clone()
    {
        // TODO implement
        throw new NotImplementedException();
    }
}
