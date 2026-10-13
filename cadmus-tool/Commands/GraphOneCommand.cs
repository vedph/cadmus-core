using Cadmus.Cli.Services;
using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.Graph;
using Cadmus.Graph.Extras;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

internal sealed class GraphOneCommand : AsyncCommand<GraphOneCommandSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context,
        GraphOneCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]MAP ITEM/PART TO GRAPH[/]");
        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        if (!string.IsNullOrEmpty(settings.RepositoryPluginTag))
        {
            AnsiConsole.MarkupLine(
                $"Repository plugin tag: [cyan]{settings.RepositoryPluginTag}[/]");
        }
        AnsiConsole.MarkupLine(
            $"{(settings.IsPart ? "Part" : "Item")} ID: [cyan]{settings.Id}[/]");

        Serilog.Log.Information("MAP TO GRAPH: " +
                     "Database: {DatabaseName}, " +
                     "Repository plugin tag: {RepositoryPluginTag}\n" +
                     "Model ID: {Id}\n",
                     settings.DatabaseName,
                     settings.RepositoryPluginTag,
                     settings.Id);

        try
        {
            // repository
            AnsiConsole.MarkupLine("Creating repository...");

            string cs = string.Format(
              CliAppContext.Configuration.GetConnectionString("Mongo")!,
              settings.DatabaseName);
            ICadmusRepository repository = CliHelper.GetCadmusRepository(
                settings.RepositoryPluginTag, cs);

            if (settings.IsDeleted)
            {
                GraphHelper.UpdateGraphForDeletion(settings.Id!,
                    settings.DatabaseName!);
                return Task.FromResult(0);
            }

            IItem? item;
            IPart? part = null;

            // get item and part
            if (settings.IsPart)
            {
                part = repository.GetPart<IPart>(settings.Id!);
                if (part == null)
                {
                    AnsiConsole.MarkupLine("[red]Part not found[/]");
                    return Task.FromResult(2);
                }
                item = repository.GetItem(part.ItemId, false);
            }
            else
            {
                item = repository.GetItem(settings.Id!, false);
            }

            if (item == null)
            {
                AnsiConsole.MarkupLine("[red]Item not found[/]");
                return Task.FromResult(2);
            }

            // update graph
            IGraphRepository graphRepository = GraphHelper.GetGraphRepository(
                settings.DatabaseName!);
            GraphUpdater updater = new(graphRepository)
            {
                // we want item-eid as an additional metadatum, derived from
                // eid in the role-less MetadataPart of the item, when present
                MetadataSupplier = new MetadataSupplier()
                    .SetCadmusRepository(repository)
                    .AddItemEid()
            };

            if (settings.Explain)
            {
                GraphUpdaterExplanation? explanation = settings.IsPart
                    ? updater.Explain(item, part!)
                    : updater.Explain(item);
                if (explanation == null)
                {
                    AnsiConsole.MarkupLine("[red]Item not found[/]");
                    return Task.FromResult(2);
                }

                AnsiConsole.MarkupLine("[yellow]Filter[/]");
                RunNodeMappingFilter f = explanation.Filter;
                AnsiConsole.MarkupLine($"- [cyan]source type[/]: {f.SourceType}");
                AnsiConsole.MarkupLine($"- [cyan]facet[/]: {f.Facet}");
                AnsiConsole.MarkupLine($"- [cyan]group[/]: {f.Group}");
                AnsiConsole.MarkupLine($"- [cyan]flags[/]: {f.Flags}");
                AnsiConsole.MarkupLine($"- [cyan]part type[/]: {f.PartType}");
                AnsiConsole.MarkupLine($"- [cyan]role[/]: {f.PartRole}");
                AnsiConsole.MarkupLineInterpolated($"- [cyan]title[/]: {f.Title}");

                AnsiConsole.MarkupLine("\n[yellow]Metadata[/]");
                foreach (string k in explanation.Metadata.Keys.OrderBy(s => s))
                {
                    AnsiConsole.MarkupLineInterpolated(
                        $"- [cyan]{k}[/]: {explanation.Metadata[k]}");
                }

                AnsiConsole.MarkupLine("\n[yellow]Mappings[/]");
                for (int i = 0; i < explanation.Mappings.Count; i++)
                {
                    AnsiConsole.Markup($"[cyan]{i + 1:000}.[/] ");
                    AnsiConsole.WriteLine(explanation.Mappings[i].ToString());
                }

                AnsiConsole.MarkupLine("\n[yellow]Nodes[/]");
                for (int i = 0; i < explanation.Set.Nodes.Count; i++)
                {
                    AnsiConsole.Markup($"[cyan]{i + 1:000}.[/] ");
                    AnsiConsole.WriteLine(explanation.Set.Nodes[i].ToString());
                }

                AnsiConsole.MarkupLine("\n[yellow]Triples[/]");
                for (int i = 0; i < explanation.Set.Triples.Count; i++)
                {
                    AnsiConsole.Markup($"[cyan]{i + 1:000}.[/] ");
                    AnsiConsole.WriteLine(explanation.Set.Triples[i].ToString());
                }
            }
            else if (settings.IsPart)
            {
                updater.Update(item, part!);
            }
            else
            {
                updater.Update(item);
            }

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return Task.FromResult(2);
        }
    }
}

internal class GraphOneCommandSettings : CommandSettings
{
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandArgument(1, "<ID>")]
    [Description("The item/part ID")]
    public string? Id { get; set; }

    [CommandOption("-g|--tag <RepositoryPluginTag>")]
    [Description("The repository factory plugin tag")]
    public string? RepositoryPluginTag { get; set; }

    [CommandOption("-p|--part")]
    [Description("The ID refers to a part rather than to an item")]
    public bool IsPart { get; set; }

    [CommandOption("-d|--deleted")]
    [Description("The ID refers to an item/part which was deleted")]
    public bool IsDeleted { get; set; }

    [CommandOption("-x|--explain")]
    [Description("Explain the graph update")]
    public bool Explain { get; set; }
}
