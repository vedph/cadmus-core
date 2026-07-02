using System.Collections.Generic;
using System.Linq;
using System.Text;
using Fusi.Tools.Data;
using System;
using Fusi.Tools;

namespace Proteus.Rendering;

/// <summary>
/// An exported segment of text. This is typically used as the payload of
/// a <see cref="TreeNode{T}"/> when building the linear tree to start the export
/// process with.
/// </summary>
public class ExportedSegment
{
    /// <summary>
    /// The name of the feature indicating that this segment was before and end
    /// of line marker (LF) in the source text.
    /// </summary>
    public const string F_EOL_TAIL = "eol-tail";

    /// <summary>
    /// Gets or sets the source identifier for this segment. When used, this
    /// usually derives from the source item for this segment.
    /// </summary>
    public int SourceId { get; set; }

    /// <summary>
    /// Gets or sets the optional type.
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    /// Gets or sets the segment's text.
    /// </summary>
    public string? Text { get; set; }

    /// <summary>
    /// Gets or sets the features linked to this segment.
    /// </summary>
    public List<StringPair>? Features { get; set; }

    /// <summary>
    /// Gets or sets the tags attached to this segment.
    /// </summary>
    public HashSet<string>? Tags { get; set; }

    /// <summary>
    /// Gets or sets optional data payload. This is a list of objects, because
    /// usually a segment just has a single object representing its data; but
    /// segments could be merged and data could thus become a list of objects
    /// from each merged segment.
    /// </summary>
    public List<object>? Payloads { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportedSegment"/>
    /// </summary>
    public ExportedSegment()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportedSegment"/>
    /// with the specified text and optional payloads.
    /// </summary>
    /// <param name="text">The text of this segment.</param>
    /// <param name="features">The optional features.</param>
    /// <param name="payloads">The optional payloads to add.</param>
    /// <exception cref="ArgumentNullException">payloads</exception>
    public ExportedSegment(string text, IEnumerable<StringPair>? features = null,
        params object[] payloads)
    {
        ArgumentNullException.ThrowIfNull(text);

        Text = text;
        if (features != null) Features = [.. features];
        if (payloads?.Length > 0) Payloads = [.. payloads];
    }

    /// <summary>
    /// Adds the specified feature. If the features list property is null,
    /// the list will be created before adding the feature to it.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    /// <param name="unique">True if the feature name must be unique in the
    /// set. In this case, if a feature with the same name exists, it will
    /// be replaced by the new one.</param>
    /// <exception cref="ArgumentNullException">name or value</exception>
    public void AddFeature(string name, string value, bool unique = false)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(value);

        if (Features != null)
        {
            // never add a feature with both name and value equal to an existing
            // feature
            if (Features.Any(f => f.Name == name && f.Value == value)) return;

            // replace existing feature with same name if unique is true
            if (unique && Features.Any(f => f.Name == name))
            {
                // remove all features with name
                foreach (StringPair feature in Features.Where(
                    f => f.Name == name).ToList())
                {
                    Features.Remove(feature);
                }
            }
        }

        Features ??= [];
        Features.Add(new StringPair(name, value));
    }

    /// <summary>
    /// Removes all the features, or all the features matching the specified
    /// name, or all the features matching the specified name and value.
    /// </summary>
    /// <param name="name">The optional name.</param>
    /// <param name="value">The optional value.</param>
    public void RemoveFeatures(string? name = null, string? value = null)
    {
        if (Features == null) return;

        if (name == null)
        {
            Features = null;
            return;
        }

        Features = [.. Features.Where(f => f.Name != name ||
            (value != null && f.Value != value))];
    }

    /// <summary>
    /// Determines whether this span has any features with the specified name
    /// or name and value when <paramref name="value"/> is not null.
    /// </summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The optional value. If not specified, only the
    /// name is matched.</param>
    /// <returns>
    ///   <c>true</c> if the specified name has the feature; otherwise,
    ///   <c>false</c>.
    /// </returns>
    /// <exception cref="ArgumentNullException">name</exception>
    public bool HasFeature(string name, string? value = null)
    {
        ArgumentNullException.ThrowIfNull(name);

        if (Features == null) return false;
        return Features.Any(f => f.Name == name &&
            (value == null || f.Value == value));
    }

    /// <summary>
    /// Clones this instance.
    /// </summary>
    /// <returns>New segment.</returns>
    public virtual ExportedSegment Clone()
    {
        return new ExportedSegment
        {
            SourceId = SourceId,
            Type = Type,
            Text = Text,
            Features = Features == null ? null : [.. Features],
            Tags = Tags == null ? null : [.. Tags],
            Payloads = Payloads == null? null : [.. Payloads],
        };
    }

    /// <summary>
    /// Converts to string.
    /// </summary>
    /// <returns>
    /// A <see cref="string" /> that represents this instance.
    /// </returns>
    public override string ToString()
    {
        StringBuilder sb = new();

        if (SourceId != 0) sb.Append('#').Append(SourceId).Append(": ");

        sb.Append(Text);
        if (Features?.Count > 0)
        {
            sb.Append(" (");
            sb.AppendJoin(", ", Features.Select(f => f.ToString()));
            sb.Append(')');
        }
        if (Tags?.Count > 0)
            sb.Append(" [").AppendJoin(",", Tags).Append(']');
        return sb.ToString();
    }

    /// <summary>
    /// Merges the source segment into the target segment, or just copy the
    /// source segment when the target is null.
    /// </summary>
    /// <param name="source">The source segment or null.</param>
    /// <param name="target">The target segment or null.</param>
    /// <returns>Merged segment or null when both segments are null.</returns>
    public static ExportedSegment? MergeSegments(ExportedSegment? source,
        ExportedSegment? target)
    {
        // nothing to do for an empty source
        if (source == null) return target;

        // just copy the source data if the target is empty
        if (target == null) return source;

        // merge text
        if (source.Text != null)
        {
            target.Text = target.Text != null ?
                target.Text + source.Text : source.Text;
        }

        // merge features
        if (source.Features != null)
        {
            foreach (var f in source.Features)
                target.AddFeature(f.Name, f.Value!);
        }

        // merge tags
        if (source.Tags != null)
        {
            if (target.Tags == null)
                target.Tags = source.Tags;
            else
                target.Tags.UnionWith(source.Tags);
        }

        // merge payloads
        if (source.Payloads != null)
        {
            if (target.Payloads == null)
                target.Payloads = source.Payloads;
            else
                target.Payloads.AddRange(source.Payloads);
        }

        return target;
    }
}
