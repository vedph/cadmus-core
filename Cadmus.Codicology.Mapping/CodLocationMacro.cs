using Cadmus.Export.Mapping;
using System.Diagnostics;
using System.Text.Json;
using Cadmus.Codicology.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Linq;

namespace Cadmus.Codicology.Mapping;

/// <summary>
/// Codicology location macro for Cadmus object mappers. This gets the JSON code
/// representing a single <see cref="CodLocation"/> object, or a single
/// <see cref="CodLocationRange"/> object, or an array of
/// <see cref="CodLocationRange"/> objects, and returns its string
/// representation.
/// Tag: <c>node-mapping-macro.cod-location</c>.
/// </summary>
[Tag("node-mapping-macro.cod-location")]
public class CodLocationMacro : INodeMappingMacro
{
    private readonly JsonSerializerOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodLocationMacro"/> class.
    /// </summary>
    public CodLocationMacro()
    {
        _options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true
        };
    }

    /// <summary>
    /// Run the macro function.
    /// </summary>
    /// <param name="context">The data context of the macro function.</param>
    /// <param name="args">The arguments: 0=the JSON code representing a single
    /// <see cref="CodLocation"/> object, or a single <see cref="CodLocationRange"/>
    /// object, or an array of <see cref="CodLocationRange"/> objects.</param>
    /// <returns>Result or null.</returns>
    /// <exception cref="ArgumentNullException">template</exception>
    public string? Run(MapperSource? context, string[]? args)
    {
        if (args?.Length != 1) return null;

        try
        {
            JsonDocument doc = JsonDocument.Parse(args[0]);

            // array of ranges
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                CodLocationRange[] ranges = JsonSerializer
                    .Deserialize<CodLocationRange[]>(args[0], _options)!;
                return string.Join(" ", ranges.Select(r => r.ToString()));
            }

            // single range (if the document has a start property)
            if (doc.RootElement.TryGetProperty("start", out _) ||
                doc.RootElement.TryGetProperty("Start", out _))
            {
                CodLocationRange? range = JsonSerializer
                    .Deserialize<CodLocationRange>(args[0], _options);
                return range?.ToString();
            }

            // else single location
            CodLocation? loc = JsonSerializer.Deserialize<CodLocation>(args[0],
                _options);

            return loc?.ToString();
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            return null;
        }
    }
}
