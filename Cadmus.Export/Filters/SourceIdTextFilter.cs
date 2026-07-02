using Fusi.Tools;
using Fusi.Tools.Configuration;
using Proteus.Core.Text;
using System;
using System.Text;

namespace Cadmus.Export.Filters;

/// <summary>
/// Source IDs renderer filter. This filter replaces all the source identifiers
/// delimited between a specified pair of opening and closing tags with
/// the corresponding mapped identifiers got from the rendering context.
/// For instance, a segment source ID with form <c>seg/itemId/nodeId</c> is mapped
/// to a target ID like <c>seg123</c>. This assumes that the source ID is prefixed
/// by the map name (e.g. <c>seg</c>) followed by a slash.
/// <para>Tag: <c>it.vedph.text-filter.str.source-id</c>.
/// Old tag: <c>it.vedph.renderer-filter.source-id</c>.</para>
/// </summary>
[Tag("it.vedph.text-filter.str.source-id")]
public sealed class SourceIdTextFilter : TextFilter<string>,
    IConfigurable<SourceIdRendererFilterOptions>
{
    private SourceIdRendererFilterOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="SourceIdTextFilter"/>
    /// class.
    /// </summary>
    public SourceIdTextFilter()
    {
        _options = new SourceIdRendererFilterOptions();
    }

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(SourceIdRendererFilterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    private static (string map, string sourceId) ParseKey(string key)
    {
        // a key has form map/layerTypeId:roleId@fragmentIndex
        // optionally followed by a suffix after the fragmentIndex
        // up to the end of the key
        int i = key.IndexOf('/');
        return i == -1
            ? ((string map, string sourceId))("", key)
            : ((string map, string sourceId))(key[..i], key[(i + 1)..]);
    }

    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="context">The optional context.</param>
    /// <returns>Filtered text or null.</returns>
    protected override object? DoApply(string? text,
        IHasDataDictionary? context = null)
    {
        if (string.IsNullOrEmpty(text) || context == null) return text;
        if (context is not CadmusRendererContext ctx) return text;

        StringBuilder sb = new();
        int start = 0, i = text.IndexOf(_options.TagOpen);
        while (i > -1)
        {
            // prepend left stuff
            if (i > start) sb.Append(text, start, i - start);

            // move to closing tag
            int j = i + _options.TagOpen.Length;
            i = text.IndexOf(_options.TagClose, i);
            if (i == -1) i = text.Length;

            // extract and resolve key if possible
            string key = text[j..i];
            (string map, string sourceId) = ParseKey(key);

            int? id = ctx.GetMappedId(map, sourceId);
            if (id != null)
            {
                sb.Append(map).Append(id);
            }
            else if (!_options.OmitUnresolved)
            {
                sb.Append(key);
            }

            // move past closing tag
            if (i < text.Length) i += _options.TagClose.Length;
            start = i;

            // move to next opening tag
            i = text.IndexOf(_options.TagOpen, i);
        }

        if (start < text.Length) sb.Append(text, start, text.Length - start);
        return sb.ToString();
    }
}

/// <summary>
/// Options for <see cref="SourceIdRendererFilterOptions"/>.
/// </summary>
public class SourceIdRendererFilterOptions
{
    /// <summary>
    /// Gets or sets the tag opening the fragment key to be mapped.
    /// </summary>
    public string TagOpen { get; set; } = "#[";

    /// <summary>
    /// Gets or sets the tag closing the fragment key to be mapped.
    /// </summary>
    public string TagClose { get; set; } = "]#";

    /// <summary>
    /// Gets or sets a value indicating whether to omit unresolved keys
    /// rather than passing them though.
    /// </summary>
    public bool OmitUnresolved { get; set; }
}
