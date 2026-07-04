using Cadmus.Core;
using Cadmus.Core.Storage;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace Cadmus.General.Parts;

/// <summary>
/// An item metadata builder which draws the title from a MetadataPart's metadatum
/// named "eid" and the description from a NotePart's text.
/// <para>Tag: <c>item-metadata-builder.eid</c>.</para>
/// </summary>
[Tag("item-metadata-builder.eid")]
public sealed class EidItemMetadataBuilder : IItemMetadataBuilder
{
    /// <summary>
    /// Builds the item's metadata specified by <paramref name="targets"/>.
    /// </summary>
    /// <param name="itemId">The ID of the item.</param>
    /// <param name="targets">The targets for which to build metadata.</param>
    /// <returns>A dictionary containing the item's metadata.</returns>
    /// <exception cref="ArgumentNullException">Thrown when
    /// <paramref name="repository"/> or <paramref name="itemId"/> is null.
    /// </exception>
    public IDictionary<string, string> Build(ICadmusRepository repository,
        string itemId,
        string targets = "TD")
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(itemId);

        Dictionary<string, string> result = [];

        if (targets.Contains('T'))
        {
            string metadataPartTag = typeof(MetadataPart)
                .GetCustomAttribute<TagAttribute>()!.Tag;

            IList<IPart> parts = repository.GetItemParts(
                [itemId], metadataPartTag);
            if (parts.Count > 0)
            {
                MetadataPart? metadataPart = parts[0] as MetadataPart;
                if (metadataPart != null)
                {
                    string? title = metadataPart.Metadata.Find(
                        m => m.Name == "eid")?.Value;
                    if (!string.IsNullOrEmpty(title)) result.Add("T", title);
                }
            }
        }

        if (targets.Contains('D'))
        {
            string notePartTag = typeof(NotePart)
                .GetCustomAttribute<TagAttribute>()!.Tag;

            IList<IPart> parts = repository.GetItemParts(
                [itemId], notePartTag);
            if (parts.Count > 0)
            {
                NotePart? notePart = parts[0] as NotePart;
                if (notePart != null && !string.IsNullOrEmpty(notePart.Text))
                    result.Add("D", notePart.Text);
            }
        }
        return result;
    }
}
