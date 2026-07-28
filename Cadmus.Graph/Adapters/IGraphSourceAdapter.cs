using System.Collections.Generic;

namespace Cadmus.Graph.Adapters;

/// <summary>
/// Graph source adapter. Implementors adapt a specific data source, like
/// item or part, to the mapping process.
/// </summary>
public interface IGraphSourceAdapter
{
    /// <summary>
    /// Adapts the specified source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <param name="metadata">The target dictionary for metadata generated
    /// by the adapter.</param>
    /// <returns>Tuple with 1=adaptation result (adapted object or null, plus
    /// the filter).</returns>
    (object? result, RunNodeMappingFilter filter) Adapt(GraphSource source,
        IDictionary<string, object> metadata);
}
