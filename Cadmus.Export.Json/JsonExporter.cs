using Cadmus.Export.Mapping;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;

namespace Cadmus.Export.Json;

/// <summary>
/// Cadmus JSON data exporter. This is a top-level class used to export Cadmus
/// data from their JSON representation to other JSON formats.
/// </summary>
public sealed class JsonExporter
{
    private readonly IItemJsonReader _reader;
    private readonly IItemIdCollector _itemIdCollector;

    /// <summary>
    /// Gets or sets the optional logger.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonExporter"/> class.
    /// </summary>
    /// <param name="reader">The item JSON reader.</param>
    /// <param name="itemIdCollector">The item ID collector.</param>
    /// <exception cref="ArgumentNullException">Null <paramref name="reader"/>
    /// or <paramref name="itemIdCollector"/>.</exception>
    public JsonExporter(IItemJsonReader reader, IItemIdCollector itemIdCollector)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _itemIdCollector = itemIdCollector
            ?? throw new ArgumentNullException(nameof(itemIdCollector));
    }

    /// <summary>
    /// The optional filter for item parts to export. If null, all parts will
    /// be exported.
    /// </summary>
    public ItemPartFilter? PartFilter { get; set; }

    /// <summary>
    /// The list of custom template filters to make available. If null, no
    /// filters will be available except for the prebuilt ones, which are always
    /// present.
    /// </summary>
    public List<IFluidFilter>? TemplateFilters { get; set; }

    /// <summary>
    /// The list of mappings to apply to the source JSON object. If empty,
    /// no mappings will be applied and the output will be empty. Else, all
    /// the matching mappings will be applied in their matching order,
    /// and their output will be merged into the final output.
    /// </summary>
    public List<NodeMapping> Mappings { get; } = [];

    /// <summary>
    /// Export the items collected by the <see cref="_itemIdCollector"/> to JSON.
    /// </summary>
    /// <param name="cancel">A cancellation token.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <returns>An asynchronous stream of JSON documents.</returns>
    public async IAsyncEnumerable<JsonDocument> ExportAsync(
        [EnumeratorCancellation] CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        ProgressReport? report = progress != null? new ProgressReport(): null;

        // for each item ID
        foreach (string itemId in _itemIdCollector.GetIds())
        {
            // get the item with its parts
            JsonDocument? doc = await _reader.ReadAsync(itemId, PartFilter);
            if (doc != null) yield return doc;

            // report progress
            if (progress != null)
            {
                report!.Count++;
                progress?.Report(report);
            }

            // cancel if requested
            if (cancel.IsCancellationRequested)
            {
                Logger?.LogInformation("Export cancelled");
                yield break;
            }
        }
    }
}
