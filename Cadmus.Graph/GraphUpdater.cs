using Cadmus.Core;
using Cadmus.Export.Mapping;
using Cadmus.Graph.Adapters;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Graph;

/// <summary>
/// Graph updater. This is a top level helper class, used to update the
/// graph whenever an item or part gets saved. An updater instance is not
/// thread-safe: use a different instance for each concurrent update.
/// </summary>
public class GraphUpdater
{
    private readonly IGraphRepository _repository;
    private readonly JsonGraphNodeMapper _mapper;
    private readonly ItemGraphSourceAdapter _itemAdapter;
    private readonly PartGraphSourceAdapter _partAdapter;

    /// <summary>
    /// Gets the metadata used by the updater. You can add more metadata
    /// manually, or set the <see cref="MetadataSupplier"/> property so that
    /// it will be invoked before each update. All metadata are automatically
    /// reset after updating.
    /// </summary>
    public IDictionary<string, object> Metadata { get; }

    /// <summary>
    /// Gets or sets the optional metadata supplier to be used by this updater.
    /// </summary>
    public MetadataSupplier? MetadataSupplier { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphUpdater"/> class.
    /// </summary>
    /// <param name="repository">The repository.</param>
    /// <exception cref="ArgumentNullException">repository</exception>
    public GraphUpdater(IGraphRepository repository)
    {
        _repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        _mapper = new JsonGraphNodeMapper
        {
            UidBuilder = repository
        };
        _itemAdapter = new ItemGraphSourceAdapter();
        _partAdapter = new PartGraphSourceAdapter();
        Metadata = new Dictionary<string, object>();
    }

    /// <summary>
    /// Adds the specified macro.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="macro">The macro.</param>
    public void AddMacro(string key, INodeMappingMacro macro)
        => _mapper.AddMacro(key, macro);

    /// <summary>
    /// Deletes the macro with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    public void DeleteMacro(string key) => _mapper.DeleteMacro(key);

    /// <summary>
    /// Sets the macros to use in this mapper.
    /// </summary>
    /// <param name="macros">The macros.</param>
    public void SetMacros(IDictionary<string, INodeMappingMacro> macros) =>
        _mapper.SetMacros(macros);

    /// <summary>
    /// Resets the macros to the builtin ones.
    /// </summary>
    public void ResetMacros() => _mapper.ResetMacros();

    private void SetMapperMetadata()
    {
        _mapper.Data.Clear();
        foreach (KeyValuePair<string, object> p in Metadata)
            _mapper.Data.Add(p.Key, p.Value);
    }

    private GraphSet Map(object data, IList<GraphNodeMapping> mappings,
        string sourceId)
    {
        GraphSet set = new();
        // the set represents the whole graph generated from this source,
        // so that anything no longer generated gets removed even if
        // nothing at all is generated now
        set.SourceIds.Add(sourceId);

        foreach (GraphNodeMapping mapping in mappings)
            _mapper.Map(data, mapping, set);

        return set;
    }

    private async Task UpdateAsync(object data, RunNodeMappingFilter filter,
        string sourceId, CancellationToken cancel)
    {
        try
        {
            SetMapperMetadata();
            _mapper.IsMappingTracingEnabled = false;
            IList<GraphNodeMapping> mappings =
                await _repository.FindMappingsAsync(filter, cancel);
            GraphSet set = Map(data, mappings, sourceId);
            await _repository.UpdateGraphAsync(set, cancel);
        }
        finally
        {
            Metadata.Clear();
        }
    }

    private GraphUpdaterExplanation Explain(object data,
        RunNodeMappingFilter filter)
    {
        SetMapperMetadata();
        _mapper.IsMappingTracingEnabled = true;
        GraphUpdaterExplanation explanation = new()
        {
            Filter = filter
        };
        foreach (GraphNodeMapping mapping in _repository.FindMappings(filter))
        {
            _mapper.Map(data, mapping, explanation.Set);
            if (_mapper.Data.TryGetValue(NodeMapper.APPLIED_MAPPING_LIST,
                out object? o) && o is IList<NodeMapping> applied)
            {
                foreach (NodeMapping a in applied)
                    explanation.Mappings.Add((GraphNodeMapping)a);
            }
            else
            {
                explanation.Mappings.Add(mapping);
            }
        }
        // copy adapted metadata to explanation metadata
        foreach (KeyValuePair<string, object> p in Metadata)
            explanation.Metadata.Add(p.Key, p.Value);

        Metadata.Clear();

        return explanation;
    }

    private (object? Data, RunNodeMappingFilter Filter) AdaptItem(IItem item)
    {
        _mapper.Context = new MapperSource(item);
        MetadataSupplier?.Supply(_mapper.Context, Metadata);
        return _itemAdapter.Adapt(_mapper.Context, Metadata);
    }

    private (object? Data, RunNodeMappingFilter Filter) AdaptPart(IItem item,
        IPart part)
    {
        // create the source
        _mapper.Context = new MapperSource(item, part);

        // supply metadata if requested
        MetadataSupplier?.Supply(_mapper.Context, Metadata);

        // adapt the source to a data object and a filter
        return _partAdapter.Adapt(_mapper.Context, Metadata);
    }

    /// <summary>
    /// Updates the graph from the specified item. The graph generated
    /// from this item replaces the one previously generated from it.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">item</exception>
    public Task UpdateAsync(IItem item, CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        (object? result, RunNodeMappingFilter filter) = AdaptItem(item);
        if (result == null)
        {
            Metadata.Clear();
            return Task.CompletedTask;
        }
        return UpdateAsync(result, filter, item.Id, cancel);
    }

    /// <summary>
    /// Updates the graph from the specified item's part. The graph generated
    /// from this part replaces the one previously generated from it.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="part">The part.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <exception cref="ArgumentNullException">item or part</exception>
    public Task UpdateAsync(IItem item, IPart part,
        CancellationToken cancel = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(part);

        (object? result, RunNodeMappingFilter filter) = AdaptPart(item, part);
        if (result == null)
        {
            Metadata.Clear();
            return Task.CompletedTask;
        }
        return UpdateAsync(result, filter, part.Id, cancel);
    }

    /// <summary>
    /// Updates the graph from the specified item. This blocks until
    /// <see cref="UpdateAsync(IItem, CancellationToken)"/> completes.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <exception cref="ArgumentNullException">item</exception>
    public void Update(IItem item) => UpdateAsync(item).GetAwaiter().GetResult();

    /// <summary>
    /// Updates the graph from the specified item's part. This blocks until
    /// <see cref="UpdateAsync(IItem, IPart, CancellationToken)"/> completes.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="part">The part.</param>
    /// <exception cref="ArgumentNullException">item or part</exception>
    public void Update(IItem item, IPart part) =>
        UpdateAsync(item, part).GetAwaiter().GetResult();

    /// <summary>
    /// Explains the update path for the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <returns>Explanation or null if item not found.</returns>
    /// <exception cref="ArgumentNullException">item</exception>
    public GraphUpdaterExplanation? Explain(IItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        (object? result, RunNodeMappingFilter filter) = AdaptItem(item);
        if (result == null)
        {
            Metadata.Clear();
            return null;
        }
        
        return Explain(result, filter);
    }

    /// <summary>
    /// Explains the update path for the specified part.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="part">The part.</param>
    /// <returns>Explanation or null if item not found.</returns>
    /// <exception cref="ArgumentNullException">item or part</exception>
    public GraphUpdaterExplanation? Explain(IItem item, IPart part)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(part);

        (object? result, RunNodeMappingFilter filter) = AdaptPart(item, part);
        if (result == null)
        {
            Metadata.Clear();
            return null;
        }

        return Explain(result, filter);
    }
}
