using Cadmus.Export.Renderers;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Cadmus.Migration.Cli.Commands;

internal sealed class JsonToXmlCommand : AsyncCommand<JsonToXmlCommandSettings>
{
    protected override Task<int> ExecuteAsync(CommandContext context,
        JsonToXmlCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[green underline]CONVERT ITEM/PART JSON TO XML[/]");

        string outputFilePath = settings.OutputFilePath ??
            Path.ChangeExtension(settings.InputFilePath, ".xml")!;

        AnsiConsole.MarkupLine($"Input: [cyan]{settings.InputFilePath}[/]");
        AnsiConsole.MarkupLine($"Output: [cyan]{outputFilePath}[/]");
        AnsiConsole.MarkupLine($"No null values: [cyan]{settings.NoNullValues}[/]");
        AnsiConsole.MarkupLine($"No false values: [cyan]{settings.NoFalseValues}[/]");
        AnsiConsole.MarkupLine($"No zero values: [cyan]{settings.NoZeroValues}[/]");
        AnsiConsole.MarkupLine($"Indented: [cyan]{settings.Indented}[/]");

        try
        {
            // load JSON
            string json = File.ReadAllText(settings.InputFilePath!);

            // write XML
            JsonToXmlConverter converter = new();
            XElement root = converter.Convert(json, new JsonToXmlConverterOptions
            {
                NoNullValues = settings.NoNullValues,
                NoFalseValues = settings.NoFalseValues,
                NoZeroValues = settings.NoZeroValues
            });
            root.Save(outputFilePath, SaveOptions.OmitDuplicateNamespaces
                | (settings.Indented
                   ? SaveOptions.None : SaveOptions.DisableFormatting));

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return Task.FromResult(2);
        }
    }
}

internal sealed class JsonToXmlCommandSettings : CommandSettings
{
    [CommandArgument(0, "<InputFilePath>")]
    [Description("The input JSON file path")]
    public string? InputFilePath { get; set; }

    [CommandOption("-o|--output")]
    [Description("The output file path (default=input with .xml)")]
    public string? OutputFilePath { get; set; }

    [CommandOption("-n|--no-null")]
    public bool NoNullValues { get; set; }

    [CommandOption("-f|--no-false")]
    public bool NoFalseValues { get; set; }

    [CommandOption("-z|--no-zero")]
    public bool NoZeroValues { get; set; }

    [CommandOption("-i|--indented")]
    public bool Indented { get; set; }
}
