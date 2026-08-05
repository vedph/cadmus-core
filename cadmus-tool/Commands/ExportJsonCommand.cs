using Cadmus.Cli.Services;
using Cadmus.Export.Json;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

internal sealed class ExportJsonCommand : AsyncCommand<ExportJsonCommandSettings>
{
    private static void ShowSettings(ExportJsonCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[green]EXPORT JSON[/]");
        AnsiConsole.WriteLine($"Config path: {settings.ConfigPath}");
        AnsiConsole.WriteLine($"Database: {settings.DatabaseName}");
        AnsiConsole.WriteLine($"Output path: {settings.OutputDirectory}");
        AnsiConsole.WriteLine($"Pretty print: {settings.PrettyPrint}");
    }

    protected override async Task<int> ExecuteAsync(CommandContext context,
        ExportJsonCommandSettings settings, CancellationToken cancellationToken)
    {
        ShowSettings(settings);

        try
        {
            string cs = string.Format(CliAppContext.Configuration
                .GetConnectionString("Mongo")!, settings.DatabaseName);

            if (!Directory.Exists(settings.OutputDirectory))
                Directory.CreateDirectory(settings.OutputDirectory);

            AnsiConsole.WriteLine("Loading exporter config...");
            string config = CliHelper.LoadFileContent(settings.ConfigPath);

            AnsiConsole.WriteLine("Building exporter factory...");
            IJsonExporterFactoryProvider provider =
                CliAppContext.GetJsonExporterFactoryProvider() ??
                throw new InvalidOperationException(
                    "JSON exporter factory provider not found.");

            JsonExporterFactory factory = provider.GetFactory(config);
            factory.ConnectionString = cs;

            AnsiConsole.WriteLine("Creating exporter...");
            JsonExporter exporter = factory.GetExporter() ??
                throw new InvalidOperationException(
                    "Unable to create JSON exporter.");

            JsonWriterOptions writerOptions = new()
            {
                Indented = settings.PrettyPrint
            };

            AnsiConsole.MarkupLine("[cyan]Exporting items...[/]");
            int n = 0;
            await foreach (JsonDocument doc in
                exporter.ExportAsync(cancellationToken))
            {
                string id = doc.RootElement.GetProperty("_id").GetString()!;
                AnsiConsole.WriteLine($" - {++n}: {id}");

                string path = Path.Combine(settings.OutputDirectory,
                    id + ".json");
                using FileStream stream = new(path, FileMode.Create,
                    FileAccess.Write);
                using Utf8JsonWriter writer = new(stream, writerOptions);
                doc.RootElement.WriteTo(writer);
            }

            AnsiConsole.MarkupLine($"[green]Done: {n} items exported.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 2;
        }
    }
}

public class ExportJsonCommandSettings : CommandSettings
{
    [CommandArgument(0, "<CONFIG>")]
    [Description("Path to the JSON exporter configuration file")]
    public string ConfigPath { get; set; } = "";

    [CommandOption("-d|--database <NAME>")]
    [Description("Database name")]
    public string DatabaseName { get; set; } = "cadmus";

    [CommandOption("-o|--output <DIRECTORY>")]
    [Description("Output directory")]
    public string OutputDirectory { get; set; } =
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "export");

    [CommandOption("--pretty-print")]
    [Description("Pretty print JSON output")]
    public bool PrettyPrint { get; set; } = false;
}
