using Cadmus.Graph;
using Fusi.Tools;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

internal sealed class GraphUpdateClassesCommand :
    AsyncCommand<UpdateGraphClassesCommandSettings>
{
    public async override Task<int> ExecuteAsync(CommandContext context,
        UpdateGraphClassesCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]UPDATE GRAPH CLASS NODES[/]");

        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine($"Profile file: [cyan]{settings.ProfilePath}[/]");

        try
        {
            IGraphRepository repository = GraphHelper.GetGraphRepository(
                settings.DatabaseName!);
            if (repository == null) return 2;

            await AnsiConsole.Progress().StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[green]Updating...[/]");
                await repository.UpdateNodeClassesAsync(CancellationToken.None,
                    new Progress<ProgressReport>(r =>
                    {
                        task.Increment(r.Percent - task.Value);
                    }));
            });
            AnsiConsole.MarkupLine("Completed.");
            return 0;
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return 2;
        }
    }
}

internal class UpdateGraphClassesCommandSettings : CommandSettings
{
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandArgument(1, "<ProfilePath>")]
    [Description("The indexer profile JSON file path")]
    public string? ProfilePath { get; set; }
}
