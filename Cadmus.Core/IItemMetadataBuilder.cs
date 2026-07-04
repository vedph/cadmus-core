using Cadmus.Core.Storage;
using System.Collections.Generic;

namespace Cadmus.Core;

/// <summary>
/// Item metadata builder interface. This is used to build item's title and/or
/// description from its parts.
/// </summary>
public interface IItemMetadataBuilder
{
    /// <summary>
    /// Build the item's metadata specified by <paramref name="targets"/>
    /// where each character is a build target: currently <c>T</c>=title and
    /// <c>D</c>=description.
    /// </summary>
    /// <param name="repository">The repository to use for building metadata.</param>
    /// <param name="itemId">The ID of the item.</param>
    /// <param name="targets">The build targets.</param>
    /// <returns>A dictionary with the built metadata (keyed by target character).
    /// </returns>
   IDictionary<string, string> Build(
       ICadmusRepository repository,
       string itemId,
       string targets = "TD");
}
