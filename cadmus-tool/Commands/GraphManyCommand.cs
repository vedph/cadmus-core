using Cadmus.Cli.Services;
using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.Graph;
using Cadmus.Graph.Extras;
using Fusi.Tools.Data;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

internal sealed class GraphManyCommand : AsyncCommand<GraphManyCommandSettings>
{
    public override Task<int> ExecuteAsync(CommandContext context,
        GraphManyCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]MAP ITEMS TO GRAPH[/]");

        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        if (!string.IsNullOrEmpty(settings.RepositoryPluginTag))
        {
            AnsiConsole.MarkupLine(
                $"Repository plugin tag: [cyan]{settings.RepositoryPluginTag}[/]");
        }
        Serilog.Log.Information("MAP TO GRAPH: " +
                     "Database: {DatabaseName}, " +
                     "Repository plugin tag: {RepositoryPluginTag}\n",
                     settings.DatabaseName,
                     settings.RepositoryPluginTag);

        try
        {
            // repository
            AnsiConsole.MarkupLine("Creating repository...");
            Serilog.Log.Information("Creating repository...");
            string cs = string.Format(
              CliAppContext.Configuration.GetConnectionString("Mongo")!,
              settings.DatabaseName);
            ICadmusRepository repository = CliHelper.GetCadmusRepository(
                settings.RepositoryPluginTag, cs);

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

            int oldPercent = 0;
            ItemFilter filter = new() { PageSize = 100 };
            DataPage<ItemInfo> page = repository.GetItems(filter);
            if (page.Total == 0) return Task.FromResult(0);

            bool error = false;
            AnsiConsole.Progress().Start(ctx =>
            {
                ProgressTask task = ctx.AddTask("Mapping items to graph");

                // first page
                do
                {
                    foreach (ItemInfo info in page.Items)
                    {
                        IItem? item = repository.GetItem(info.Id!, true);
                        if (item == null) continue;
                        if (item == null)
                        {
                            AnsiConsole.MarkupLine("[red]Item not found[/]");
                            error = true;
                            break;
                        }

                        // update graph for item
                        updater.Update(item);

                        // update graph for its parts
                        foreach (IPart part in item.Parts)
                            updater.Update(item, part);
                    }

                    // progress
                    int percent = filter.PageNumber * 100 / page.PageCount;
                    if (percent != oldPercent)
                    {
                        task.Increment(percent - oldPercent);
                        oldPercent = percent;
                    }

                    // next page
                    if (++filter.PageNumber > page.PageCount) break;
                    page = repository.GetItems(filter);
                } while (!error && page.Items.Count != 0);
            });

            return error ? Task.FromResult(2) : Task.FromResult(0);
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return Task.FromResult(2);
        }
    }
}

internal class GraphManyCommandSettings : CommandSettings
{
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandOption("-g|--tag <RepositoryPluginTag>")]
    [Description("The repository factory plugin tag")]
    public string? RepositoryPluginTag { get; set; }
}
