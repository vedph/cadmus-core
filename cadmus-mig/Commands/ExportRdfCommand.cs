using Cadmus.Export.Rdf;
using Cadmus.Migration.Cli.Services;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Migration.Cli.Commands;

internal sealed class ExportRdfCommand : AsyncCommand<ExportRdfCommandSettings>
{
    private static void ShowSettings(ExportRdfCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[green]EXPORT RDF[/]");
        AnsiConsole.WriteLine($"Database: {settings.DatabaseName}");
        AnsiConsole.WriteLine($"Output path: {settings.OutputPath}");
        AnsiConsole.WriteLine($"Format: {settings.Format}");
        AnsiConsole.WriteLine($"Include prefixes: {settings.IncludePrefixes}");
        AnsiConsole.WriteLine($"Include comments: {settings.IncludeComments}");
        AnsiConsole.WriteLine($"Base URI: {settings.BaseUri ?? "-"}");
        AnsiConsole.WriteLine($"Batch size: {settings.BatchSize}");
        AnsiConsole.WriteLine($"Pretty print: {settings.PrettyPrint}");
        AnsiConsole.WriteLine($"Export referenced nodes only: " +
            $"{settings.ExportReferencedNodesOnly}");
        AnsiConsole.WriteLine("Node tag filter: " +
            $"{(settings.NodeTagFilter != null &&
            settings.NodeTagFilter.Count != 0
            ? string.Join(", ", settings.NodeTagFilter) : "-")}");
        AnsiConsole.WriteLine("Triple tag filter: " +
            $"{(settings.TripleTagFilter != null &&
            settings.TripleTagFilter.Count != 0
            ? string.Join(", ", settings.TripleTagFilter) : "-")}");
        AnsiConsole.WriteLine($"Encoding: {settings.Encoding}\n");
    }

    protected override async Task<int> ExecuteAsync(CommandContext context,
        ExportRdfCommandSettings settings, CancellationToken cancel)
    {
        ShowSettings(settings);

        try
        {
            string cs = string.Format(ConfigurationService.Configuration!
                .GetConnectionString("Graph")!, settings.DatabaseName);

            RdfExporter exporter = new(cs, new RdfExportSettings
                {
                    Format = settings.Format,
                    IncludePrefixes = settings.IncludePrefixes,
                    IncludeComments = settings.IncludeComments,
                    BaseUri = settings.BaseUri,
                    BatchSize = settings.BatchSize,
                    PrettyPrint = settings.PrettyPrint,
                    ExportReferencedNodesOnly = settings.ExportReferencedNodesOnly,
                    NodeTagFilter = settings.NodeTagFilter,
                    TripleTagFilter = settings.TripleTagFilter,
                    Encoding = Encoding.GetEncoding(settings.Encoding)
                });
            exporter.OnProgressReported += progress =>
            {
                AnsiConsole.WriteLine($"  {progress.ProcessedTriples}/" +
                    $"{progress.TotalTriples}");
            };
            await exporter.ExportAsync(settings.OutputPath);

            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 2;
        }
    }
}

public class ExportRdfCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the name of the database to read items from.
    /// </summary>
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The name of the source Cadmus database.")]
    public required string DatabaseName { get; set; }

    /// <summary>
    /// The output file path.
    /// </summary>
    [CommandArgument(1, "<OutputPath>")]
    [Description("The output file path.")]
    public required string OutputPath { get; set; }

    /// <summary>
    /// The RDF format to export (turtle, rdfxml, ntriples, jsonld).
    /// Default is "turtle".
    /// </summary>
    [CommandOption("-f|--format <FORMAT>")]
    [Description("The RDF format to export (turtle, rdfxml, rdfowlxml, " +
        "ntriples, jsonld). Default is 'turtle'.")]
    [DefaultValue("turtle")]
    public string Format { get; set; } = "turtle";

    /// <summary>
    /// Whether to include prefix declarations in the output.
    /// Default is true.
    /// </summary>
    [CommandOption("-p|--include-prefixes")]
    [Description("Whether to include prefix declarations in the output. " +
        "Default is true.")]
    [DefaultValue(true)]
    public bool IncludePrefixes { get; set; } = true;

    /// <summary>
    /// Whether to include comments in the output.
    /// Default is true.
    /// </summary>
    [CommandOption("-c|--include-comments")]
    [Description("Whether to include comments in the output. Default is true.")]
    [DefaultValue(true)]
    public bool IncludeComments { get; set; } = true;

    /// <summary>
    /// The base URI to use for relative URIs.
    /// If null or empty, no base URI is used.
    /// </summary>
    [CommandOption("--base-uri <URI>")]
    [Description("The base URI to use for relative URIs. If null or empty, " +
        "no base URI is used.")]
    public string? BaseUri { get; set; }

    /// <summary>
    /// Maximum number of triples to process in a single batch.
    /// Default is 10000.
    /// </summary>
    [CommandOption("--batch-size <SIZE>")]
    [Description("Maximum number of triples to process in a single batch. " +
        "Default is 10000.")]
    [DefaultValue(10000)]
    public int BatchSize { get; set; } = 10000;

    /// <summary>
    /// Whether to pretty-print the output (add indentation and line breaks).
    /// Default is true.
    /// </summary>
    [CommandOption("-r|--pretty-print")]
    [Description("Whether to pretty-print the output (add indentation and " +
        "line breaks). Default is true.")]
    [DefaultValue(true)]
    public bool PrettyPrint { get; set; } = true;

    /// <summary>
    /// Whether to export only nodes that are referenced in triples.
    /// Default is false (exports all nodes).
    /// </summary>
    [CommandOption("--export-referenced-nodes-only")]
    [Description("Whether to export only nodes that are referenced in triples. " +
        "Default is false (exports all nodes).")]
    public bool ExportReferencedNodesOnly { get; set; }

    /// <summary>
    /// Optional filter for node tags. If specified, only nodes with matching
    /// tags are exported.
    /// </summary>
    [CommandOption("--node-tag-filter <TAGS>")]
    [Description("Optional filter for node tags. If specified, only nodes " +
        "with matching tags are exported. Comma-separated.")]
    public HashSet<string>? NodeTagFilter { get; set; }

    /// <summary>
    /// Optional filter for triple tags. If specified, only triples with
    /// matching tags are exported.
    /// </summary>
    [CommandOption("--triple-tag-filter <TAGS>")]
    [Description("Optional filter for triple tags. If specified, only triples " +
        "with matching tags are exported. Comma-separated.")]
    public HashSet<string>? TripleTagFilter { get; set; }

    /// <summary>
    /// The character encoding to use for output files.
    /// Default is UTF-8.
    /// </summary>
    [CommandOption("--encoding <ENCODING>")]
    [Description("The character encoding to use for output files. " +
        "Default is UTF-8.")]
    [DefaultValue("UTF-8")]
    public string Encoding { get; set; } = "UTF-8";
}
