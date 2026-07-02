using Cadmus.Export;
using Cadmus.Migration.Cli.Services;
using Fusi.Tools;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Migration.Cli.Commands;

internal sealed class DumpThesauriCommand : AsyncCommand<DumpThesauriCommandSettings>
{
    private static void ShowSettings(DumpThesauriCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[green]DUMP THESAURI[/]");
        AnsiConsole.MarkupLine($"DatabaseName: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine($"OutputPath: [cyan]{settings.OutputPath}[/]");
        AnsiConsole.MarkupLine($"Indented: [cyan]{settings.Indented}[/]");
    }

    protected override async Task<int> ExecuteAsync(CommandContext context,
        DumpThesauriCommandSettings settings, CancellationToken cancel)
    {
        ShowSettings(settings);
        try
        {
            // get connection string
            string connectionString = string.Format(CultureInfo.InvariantCulture,
                ConfigurationService.Configuration!.GetConnectionString("Default")!,
                settings.DatabaseName);

            MongoThesaurusDumper dumper = new(
                new MongoThesaurusDumperOptions
                {
                    ConnectionString = connectionString,
                    DatabaseName = settings.DatabaseName,
                    OutputPath = settings.OutputPath,
                    Indented = settings.Indented
                });

            AnsiConsole.MarkupLine("Dumping thesauri...");
            
            int count = await dumper.DumpAsync(CancellationToken.None,
                new Progress<ProgressReport>(r =>
                {
                    // only report every 10th item to reduce noise
                    if (r.Count % 10 == 0 || r.Count == 1)
                    {
                        AnsiConsole.MarkupLine(
                            $"Processed [yellow]{r.Count}[/] thesauri...");
                    }
                }));

            AnsiConsole.MarkupLine($"[green]Completed![/] Dumped " +
                $"[yellow]{count}[/] thesauri to [cyan]{settings.OutputPath}[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return 2;
        }
    }
}

public class DumpThesauriCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the name of the database to read items from.
    /// </summary>
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The source Cadmus database.")]
    public required string DatabaseName { get; set; }

    [CommandOption("-o|--output")]
    [Description("The output dump file path")]
    public string OutputPath { get; set; } = Path.Combine(
        Environment.GetFolderPath
            (Environment.SpecialFolder.DesktopDirectory),
            "thesauri.json");

    [CommandOption("--indented")]
    [Description("Indent JSON output.")]
    public bool Indented { get; set; }
}
