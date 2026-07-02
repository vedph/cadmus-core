using Fusi.Tools.Data;
using Proteus.Rendering;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Cadmus.Export.Filters;

/// <summary>
/// A tree node payload version tagger for <see cref="ExportedSegment"/>'s.
/// This is used to adapt the <see cref="ExportedSegment"/> payload to the generic
/// <see cref="TreeNodeVersionMerger{T}"/>.
/// </summary>
public sealed class SpanTreeNodePayloadTagger :
    ITreeNodePayloadTagger<ExportedSegment>
{
    /// <summary>
    /// Adds <paramref name="tag" /> to the specified payload data.
    /// </summary>
    /// <param name="data">The data to add the tag to.</param>
    /// <param name="tag">The tag to add.</param>
    /// <exception cref="ArgumentNullException">data or tag</exception>
    public void AddTag(ExportedSegment data, string tag)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tag);

        data.AddFeature(AppParallelTextTreeFilter.FN_VERSION_TAG, tag);
    }

    /// <summary>
    /// Clears the tags from the specified data.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <exception cref="ArgumentNullException">data</exception>
    public void ClearTags(ExportedSegment data)
    {
        ArgumentNullException.ThrowIfNull(data);
        data.RemoveFeatures(AppParallelTextTreeFilter.FN_VERSION_TAG);
    }

    /// <summary>
    /// Clones the payload.
    /// </summary>
    /// <param name="data">The data or null.</param>
    /// <returns>Cloned data or null.</returns>
    [return: NotNullIfNotNull(nameof(data))]
    public ExportedSegment? ClonePayload(ExportedSegment? data) => data?.Clone();

    /// <summary>
    /// Gets the tag(s) from the specified payload.
    /// </summary>
    /// <param name="data">The payload data.</param>
    /// <returns>Tags.</returns>
    public IList<string> GetTags(ExportedSegment data)
    {
        if (data.Features == null) return [];

        return [.. data.Features
            .Where(f => f.Name == AppParallelTextTreeFilter.FN_VERSION_TAG)
            .Select(f => f.Value ?? "")];
    }

    /// <summary>
    /// Determines whether the specified payload data has a comparable value.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <returns>
    /// <c>true</c> if has payload value; otherwise, <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">data</exception>
    public bool HasPayloadValue(ExportedSegment data)
    {
        ArgumentNullException.ThrowIfNull(data);
        return data.Text != null;
    }

    /// <summary>
    /// Determines whether the specified data has the specified tag.
    /// </summary>
    /// <param name="data">The data.</param>
    /// <param name="tag">The tag.</param>
    /// <returns>
    /// <c>true</c> if the specified data has tag; otherwise,
    /// <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">tag</exception>
    public bool HasTag(ExportedSegment data, string tag)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentNullException.ThrowIfNull(tag);

        return data.HasFeature(
            AppParallelTextTreeFilter.FN_VERSION_TAG,
            tag);
    }

    /// <summary>
    /// Matches the payload values.
    /// </summary>
    /// <param name="tag">The current tag. This can be used when payload values
    /// depend on tag.</param>
    /// <param name="a">The first payload.</param>
    /// <param name="b">The second payload.</param>
    /// <returns>
    /// True if payload values match.
    /// </returns>
    /// <exception cref="ArgumentNullException">a or b</exception>
    public bool MatchPayloadValues(string tag, ExportedSegment a, ExportedSegment b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        return a.Text == b.Text;
    }
}
