using System;
using Cadmus.Core.Config;

namespace Cadmus.Import;

/// <summary>
/// A generic facet reader.
/// </summary>
public interface IFacetReader : IDisposable
{
    /// <summary>
    /// The current facet definition read from source, or null.
    /// </summary>
    FacetDefinition? Current { get; }

    /// <summary>
    /// Read the next facet definition from source.
    /// </summary>
    /// <returns>True if read, false if no more facets in source.</returns>
    bool Next();
}
