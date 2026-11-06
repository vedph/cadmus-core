using Cadmus.Graph;
using System.Collections.Generic;

namespace Cadmus.GraphStudio.Api.Models;

/// <summary>
/// The result of running mappings: the graph set or an error, plus any
/// warnings about the mappings (e.g. filters which would prevent them from
/// being applied in Cadmus to a source with the specified metadata).
/// </summary>
public sealed class RunMappingResult : ErrorWrapper<GraphSet>
{
    public List<string> Warnings { get; set; } = [];
}
