using Cadmus.Cli.Services;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

internal sealed class RunMongoScriptCommand :
    AsyncCommand<RunMongoScriptCommandSettings>
{
    private static void ShowResult(MongoScriptExecutionResult result)
    {
        if (result.Success)
        {
            AnsiConsole.MarkupLine($"[green]Script executed successfully " +
                $"in {result.ExecutionTimeMs} ms.[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]Script failed[/]");

            if (result.ErrorMessage != null)
                AnsiConsole.MarkupLine($"-error: [red]{result.ErrorMessage}[/]");
            if (result.FullErrorDetails != null)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine(
                    $"- error details: [red]{result.FullErrorDetails}[/]");
            }
        }

        if (result.CommandResults?.Count > 0)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]RESULTS[/]");

            int n = 1;
            foreach (MongoCommandExecutionResult cmdResult in
                result.CommandResults)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.Write($"- {n++} ");
                AnsiConsole.MarkupLine($"[yellow]{cmdResult.OriginalCommand}[/]");

                if (cmdResult.Success)
                {
                    AnsiConsole.MarkupLine("  : [green]success[/]");
                    if (cmdResult.Result != null)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine(
                            $"- result: [yellow]{cmdResult.Result}[/]");
                    }
                }
                else
                {
                    AnsiConsole.MarkupLine("  : [red]failure[/]");
                    if (cmdResult.ErrorMessage != null)
                        AnsiConsole.MarkupLine($"  - error: [red]{cmdResult.ErrorMessage}[/]");
                    if (cmdResult.FullErrorDetails != null)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine(
                            $"  - error details: [red]{cmdResult.FullErrorDetails}[/]");
                    }
                }
            }
        }
    }

    public override async Task<int> ExecuteAsync(CommandContext context,
        RunMongoScriptCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]RUN MONGO SCRIPT[/]");
        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine(
            $"Script: [cyan]{settings.Script ?? settings.ScriptFilePath}[/]");

        // nope if no script
        if (string.IsNullOrWhiteSpace(settings.Script) &&
            string.IsNullOrWhiteSpace(settings.ScriptFilePath))
        {
            AnsiConsole.MarkupLine("[red]No script provided[/]");
            return 1;
        }

        // prompt for confirmation
        if (!settings.IsConfirmed && !AnsiConsole.Confirm("Run the script?", false))
        {
            AnsiConsole.MarkupLine("[yellow]Operation cancelled[/]");
            return 0;
        }

        // run the script
        try
        {
            string cs = string.Format(
                CliAppContext.Configuration.GetConnectionString("Mongo")!,
                settings.DatabaseName);

            string script = !string.IsNullOrWhiteSpace(settings.ScriptFilePath)
                ? await File.ReadAllTextAsync(settings.ScriptFilePath)
                : settings.Script!;

            MongoScriptRunner runner = new(cs, settings.DatabaseName);
            MongoScriptExecutionResult result = await runner.RunScriptAsync(script);

            AnsiConsole.WriteLine();
            ShowResult(result);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            AnsiConsole.WriteLine(ex.ToString());
            return 1;
        }

        return 0;
    }
}

public class RunMongoScriptCommandSettings : CommandSettings
{
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The database name")]
    public string DatabaseName { get; set; } = "";

    [CommandOption("-s|--script <Script>")]
    [Description("The script to run")]
    public string? Script { get; set; }

    [CommandOption("-f|--file <FilePath>")]
    [Description("The script file to run")]
    public string? ScriptFilePath { get; set; }

    [CommandOption("-y|--confirm")]
    [Description("Confirm without prompting")]
    public bool IsConfirmed { get; set; }
}
