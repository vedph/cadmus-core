using Cadmus.Export.Mapping;
using Fusi.Tools;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Nodes;
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
    /// The optional custom template filters to make available to the Fluid
    /// templates used to render <see cref="Mappings"/>' output, keyed by
    /// the Fluid filter keyword each of them is to be invoked with from a
    /// template (e.g. a filter under key <c>historical-date</c> is used as
    /// <c>{{ value | historical-date }}</c>). If null, no custom filters
    /// will be available besides Fluid's own built-in ones (e.g. <c>json</c>).
    /// </summary>
    public IDictionary<string, IFluidFilter>? TemplateFilters { get; set; }

    /// <summary>
    /// The list of mappings to apply to the source JSON object. If empty,
    /// no mappings will be applied and the output will be empty. Else, all
    /// the matching mappings will be applied in their matching order,
    /// and their output will be merged into the final output.
    /// </summary>
    public List<NodeMapping> Mappings { get; } = [];

    /// <summary>
    /// Export the items collected by the <see cref="_itemIdCollector"/> to
    /// JSON, transforming each item's source JSON via <see cref="Mappings"/>
    /// (rendered by an internal <see cref="JsonTemplateNodeMapper"/>, made
    /// aware of <see cref="TemplateFilters"/>) into its target JSON.
    /// </summary>
    /// <param name="cancel">A cancellation token.</param>
    /// <param name="progress">An optional progress reporter.</param>
    /// <returns>An asynchronous stream of JSON documents.</returns>
    public async IAsyncEnumerable<JsonDocument> ExportAsync(
        [EnumeratorCancellation] CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        ProgressReport? report = progress != null? new ProgressReport(): null;

        JsonTemplateNodeMapper mapper = new()
        {
            Logger = Logger
        };
        if (TemplateFilters != null) mapper.SetFilters(TemplateFilters);

        // for each item ID
        foreach (string itemId in _itemIdCollector.GetIds())
        {
            // get the item with its parts
            using JsonDocument? doc = await _reader.ReadAsync(itemId, PartFilter);
            if (doc != null)
            {
                // apply all the mappings, merging their output into target
                JsonObject target = [];
                string json = doc.RootElement.GetRawText();
                foreach (NodeMapping mapping in Mappings)
                    mapper.Map(json, mapping, target);

                yield return JsonDocument.Parse(target.ToJsonString());
            }

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
