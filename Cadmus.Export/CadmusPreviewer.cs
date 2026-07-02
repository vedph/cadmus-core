using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.Export.Config;
using Fusi.Tools.Data;
using Proteus.Rendering;
using Proteus.Rendering.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Cadmus.Export;

/// <summary>
/// Cadmus object previewer. This is a high level class using rendition
/// components to render a preview for Cadmus parts or fragments.
/// </summary>
public sealed class CadmusPreviewer
{
    /// <summary>
    /// The separator used in the keys of the JSON renderers in the configuration
    /// document. This is used to separate the part type ID from the role ID
    /// and corresponds to <c>:</c>.
    /// </summary>
    public const char RENDERER_KEY_SEPARATOR = ':';

    private readonly ICadmusRepository? _repository;
    private readonly CadmusRenderingFactory _factory;
    private readonly BlockLinearTextTreeFilter _blockFilter;
    // cache
    private readonly Dictionary<string, IJsonRenderer> _jsonRenderers;
    private readonly Dictionary<string, ITextPartFlattener> _flatteners;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadmusPreviewer"/> class.
    /// </summary>
    /// <param name="factory">The factory.</param>
    /// <param name="repository">The optional repository. You should always
    /// pass a repository, unless you are just consuming the methods using
    /// JSON as their input.</param>
    /// <exception cref="ArgumentNullException">repository or factory</exception>
    public CadmusPreviewer(CadmusRenderingFactory factory,
        ICadmusRepository? repository)
    {
        _factory = factory ??
            throw new ArgumentNullException(nameof(factory));
        _repository = repository;
        _blockFilter = new BlockLinearTextTreeFilter();

        // cached components
        _jsonRenderers = [];
        _flatteners = [];
    }

    /// <summary>
    /// Gets all the keys registered for JSON renderers in the
    /// configuration of this factory. This is used by client code
    /// to determine for which Cadmus objects a preview is available.
    /// </summary>
    /// <returns>List of unique keys.</returns>
    public HashSet<string> GetJsonRendererKeys()
        => _factory.GetJsonRendererKeys();

    /// <summary>
    /// Gets all the keys registered for JSON text part flatteners
    /// in the configuration of this factory. This is used by client code
    /// to determine for which Cadmus objects a preview is available.
    /// </summary>
    /// <returns>List of unique keys.</returns>
    public HashSet<string> GetFlattenerKeys()
        => _factory.GetFlattenerKeys();

    /// <summary>
    /// Gets all the keys registered for item composers in the configuration
    /// of this factory.
    /// </summary>
    /// <returns>List of unique keys.</returns>
    public HashSet<string> GetComposerKeys()
        => _factory.GetComposerKeys();

    private IJsonRenderer? GetRendererFromKey(string key)
    {
        IJsonRenderer? renderer;

        if (_jsonRenderers.TryGetValue(key, out IJsonRenderer? value))
        {
            renderer = value;
        }
        else
        {
            renderer = _factory.GetJsonRenderer(key);
            if (renderer == null) return null;
            _jsonRenderers[key] = renderer;
        }
        return renderer;
    }

    private CadmusRendererContext BuildContext(IItem item)
    {
        // if item has no parts, get at least the part for base text, if any
        if (_repository != null && item.Parts.Count == 0)
        {
            IPart? textPart = _repository.GetItemParts(
                [item.Id], null, PartBase.BASE_TEXT_ROLE_ID)
                .FirstOrDefault();
            if (textPart != null) item.Parts.Add(textPart);
        }

        // create context
        CadmusRendererContext context = new()
        {
            Repository = _repository,
            Source = item
        };

        // supply metadata
        context.Data[ItemComposer.M_ITEM_ID] = item.Id;
        context.Data[ItemComposer.M_ITEM_FACET] = item.FacetId;

        if (!string.IsNullOrEmpty(item.GroupId))
            context.Data[ItemComposer.M_ITEM_GROUP] = item.GroupId;

        context.Data[ItemComposer.M_ITEM_TITLE] = item.Title;

        if (item.Flags != 0)
            context.Data[ItemComposer.M_ITEM_FLAGS] = item.Flags;

        return context;
    }

    /// <summary>
    /// Renders the JSON code representing a part.
    /// </summary>
    /// <param name="json">The JSON code representing the part's content.</param>
    /// <returns>Rendition or empty string.</returns>
    /// <param name="context">The optional renderer context.</param>
    /// <exception cref="ArgumentNullException">json</exception>
    public string RenderPartJson(string json, CadmusRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(json);

        // get part type ID
        JsonDocument doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("typeId", out JsonElement typeIdElem))
            return "";
        string? typeId = typeIdElem.GetString();
        if (typeId == null) return "";

        // get the renderer targeting the part type ID
        IJsonRenderer? renderer = GetRendererFromKey(typeId);

        // render
        return renderer != null ? renderer.Render(json, context) : "";
    }

    /// <summary>
    /// Renders the part with the specified ID, using the renderer targeting
    /// its part type ID.
    /// Note that this method requires a repository.
    /// </summary>
    /// <param name="itemId">The item's identifier. This is used to get
    /// item's metadata, eventually consumed by filters. If there is no
    /// repository, or the item is not found, no context will be created
    /// and passed to filters.</param>
    /// <param name="partId">The part's identifier.</param>
    /// <returns>Rendition or empty string.</returns>
    /// <exception cref="ArgumentNullException">itemId or partId</exception>
    public string RenderPart(string itemId, string partId)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentNullException.ThrowIfNull(partId);

        IItem? item = _repository?.GetItem(itemId, false);
        if (item == null) return "";

        CadmusRendererContext context = BuildContext(item);

        string? json = _repository?.GetPartContent(partId);
        if (json == null) return "";

        return RenderPartJson(json, context);
    }

    private static JsonElement? GetFragmentAt(JsonElement fragments, int index)
    {
        if (index >= fragments.GetArrayLength()) return null;

        int i = 0;
        foreach (JsonElement fr in fragments.EnumerateArray())
        {
            if (i == index) return fr;
            i++;
        }
        return null;
    }

    /// <summary>
    /// Renders the specified fragment's JSON, representing a layer part.
    /// </summary>
    /// <param name="json">The JSON code representing the layer part's
    /// content.</param>
    /// <param name="frIndex">Index of the fragment in the <c>fragments</c>
    /// array of the received layer part.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Rendition or empty string.</returns>
    /// <exception cref="ArgumentNullException">json or context</exception>
    public string RenderFragmentJson(string json, int frIndex,
        CadmusRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(json);
        ArgumentNullException.ThrowIfNull(context);

        // get the part type ID and role ID (=fragment type)
        JsonDocument doc = JsonDocument.Parse(json);

        // type
        if (!doc.RootElement.TryGetProperty("typeId",
            out JsonElement typeIdElem))
        {
            return "";
        }
        string? typeId = typeIdElem.GetString();
        if (typeId == null) return "";

        // role
        if (!doc.RootElement.TryGetProperty("roleId",
            out JsonElement roleIdElem))
        {
            return "";
        }
        string? roleId = roleIdElem.GetString();
        if (roleId == null) return "";

        // the target ID is the combination of these two IDs
        string key = $"{typeId}{RENDERER_KEY_SEPARATOR}{roleId}";

        IJsonRenderer? renderer = GetRendererFromKey(key);

        // extract the targeted fragment
        if (!doc.RootElement.TryGetProperty("fragments",
            out JsonElement fragments))
        {
            return "";
        }
        JsonElement? fr = GetFragmentAt(fragments, frIndex);
        if (fr == null) return "";

        // render
        string frJson = fr.ToString()!;
        return renderer != null ? renderer.Render(frJson, context) : "";
    }

    /// <summary>
    /// Renders the fragment at index <paramref name="frIndex"/> in the part
    /// with ID <paramref name="partId"/>, using the renderer targeting
    /// its part role ID.
    /// Note that this method requires a repository.
    /// </summary>
    /// <param name="itemId">The item's identifier. This is used to get
    /// item's metadata, eventually consumed by filters. If there is no
    /// repository, or the item is not found, no context will be created
    /// and passed to filters.</param>
    /// <param name="partId">The part's identifier.</param>
    /// <returns>Rendition or empty string.</returns>
    /// <param name="frIndex">The fragment's index in the layer part's
    /// fragments array.</param>
    /// <exception cref="ArgumentNullException">itemId or partId</exception>
    /// <exception cref="ArgumentOutOfRangeException">frIndex less than 0
    /// </exception>
    public string RenderFragment(string itemId, string partId, int frIndex)
    {
        ArgumentNullException.ThrowIfNull(itemId);
        ArgumentNullException.ThrowIfNull(partId);
        ArgumentOutOfRangeException.ThrowIfNegative(frIndex);

        string? json = _repository?.GetPartContent(partId);
        if (json == null) return "";

        IItem? item = _repository?.GetItem(itemId, false);
        if (item == null) return "";

        CadmusRendererContext? context = BuildContext(item);

        return RenderFragmentJson(json, frIndex, context);
    }

    /// <summary>
    /// Builds an array of segments of text from the specified text part.
    /// Note that this method requires a repository.
    /// </summary>
    /// <param name="textPartId">The text part identifier.</param>
    /// <param name="layerPartIds">The IDs of the layers to include in the
    /// rendition.</param>
    /// <returns>Array of segments.</returns>
    /// <exception cref="ArgumentNullException">textPartId or layerPartIds
    /// </exception>
    public IList<ExportedSegment> BuildTextSegments(string textPartId,
        IList<string> layerPartIds)
    {
        ArgumentNullException.ThrowIfNull(textPartId);
        ArgumentNullException.ThrowIfNull(layerPartIds);

        // get the part's JSON
        if (_repository == null) return [];
        string? json = _repository.GetPartContent(textPartId);
        if (json == null) return [];

        // get the part type ID (role ID is always base-text)
        JsonDocument doc = JsonDocument.Parse(json);
        string? typeId = doc.RootElement.GetProperty("typeId").GetString();
        if (typeId == null) return [];

        // get the flattener for that type ID (reuse from cached if available)
        ITextPartFlattener? flattener;
        if (_flatteners.TryGetValue(typeId, out ITextPartFlattener? value))
        {
            flattener = value;
        }
        else
        {
            flattener = _factory.GetTextPartFlattener(typeId);
            if (flattener == null) return [];
            _flatteners[typeId] = flattener;
        }

        // load part and layers
        IPart? part = _repository?.GetPart<IPart>(textPartId);
        if (part == null) return [];

        // load item
        IItem? item = _repository!.GetItem(part.ItemId, false);
        if (item == null) return [];

        List<IPart> layerParts = [.. layerPartIds
            .Select(lid => _repository!.GetPart<IPart>(lid)!)
            .Where(p => p != null)];

        // flatten them
        Tuple<string, IList<AnnotatedTextRange>> tr = flattener.Flatten(
            part, layerParts);

        // merge ranges
        IList<AnnotatedTextRange> mergedRanges =
            AnnotatedTextRange.GetConsecutiveRanges(
                0, tr.Item1.Length - 1, tr.Item2);

        // assign text to merged ranges
        foreach (AnnotatedTextRange range in mergedRanges)
            range.AssignText(tr.Item1);

        // build a linear tree from merged ranges
        TreeNode<ExportedSegment> tree =
            CadmusTextTreeBuilder.BuildTreeFromRanges(mergedRanges, tr.Item1);
        if (!tree.HasChildren) return [];

        // apply linear tree block filter
        tree = _blockFilter.Apply(tree, item);

        // collect payloads from tree nodes and return them
        List<ExportedSegment> segments = [];
        tree.Traverse(node =>
        {
            if (node.Data != null) segments.Add(node.Data);
            return true;
        });

        return segments;
    }
}
