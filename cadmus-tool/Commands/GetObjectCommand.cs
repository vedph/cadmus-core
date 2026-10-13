using Cadmus.Cli.Services;
using Cadmus.Core;
using Cadmus.Core.Storage;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace Cadmus.Cli.Commands;

internal sealed class GetObjectCommand : AsyncCommand<GetObjectCommandSettings>
{
    private static void WriteText(string path, string text)
    {
        using StreamWriter writer = new(
            new FileStream(path, FileMode.Create, FileAccess.Write,
            FileShare.Read), Encoding.UTF8);
        writer.WriteLine(text);
        writer.Flush();
    }

    public override Task<int> ExecuteAsync(CommandContext context,
        GetObjectCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[green underline]GET OBJECT[/]");

        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine($"ID: [cyan]{settings.Id}[/]");
        if (!string.IsNullOrEmpty(settings.RepositoryPluginTag))
        {
            AnsiConsole.MarkupLine(
                $"Repository plugin tag: [cyan]{settings.RepositoryPluginTag}[/]");
        }
        AnsiConsole.MarkupLine($"Output: [cyan]{settings.OutputDir}[/]");
        AnsiConsole.MarkupLine(
            $"Item/part: [cyan]{(settings.IsPart ? "part" : "item")}[/]");
        AnsiConsole.MarkupLine($"XML: [cyan]{(settings.IsXml ? "yes" : "no")}[/]");
        Serilog.Log.Information("GET OBJECT: " +
                          "Database: {DatabaseName}, " +
                          "ID: {Id}, " +
                          "Repository plugin tag: {RepositoryPluginTag}, " +
                          "Output: {OutputDir}, " +
                          "IsPart: {IsPart}, " +
                          "XML: {IsXml}",
                          settings.DatabaseName,
                          settings.Id,
                          settings.RepositoryPluginTag,
                          settings.OutputDir,
                          settings.IsPart,
                          settings.IsXml);

        try
        {
            if (!Directory.Exists(settings.OutputDir))
                Directory.CreateDirectory(settings.OutputDir!);

            // repository
            AnsiConsole.WriteLine("Creating repository...");
            string cs = string.Format(
                CliAppContext.Configuration.GetConnectionString("Mongo")!,
                settings.DatabaseName);
            ICadmusRepository repository = CliHelper.GetCadmusRepository(
                settings.RepositoryPluginTag, cs);

            // get object
            string? json;
            if (settings.IsPart)
            {
                json = repository.GetPartContent(settings.Id!);
                if (json == null)
                {
                    AnsiConsole.MarkupLine("[red]Part not found[/]");
                    return Task.FromResult(2);
                }
            }
            else
            {
                IItem? item = repository.GetItem(settings.Id!, false);
                if (item == null)
                {
                    AnsiConsole.MarkupLine("[red]Item not found[/]");
                    return Task.FromResult(2);
                }
                json = JsonSerializer.Serialize(item, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
            }

            // write JSON
            string path = Path.Combine(settings.OutputDir ?? "",
                settings.Id + ".json");
            AnsiConsole.MarkupLine("[yellow]  -> [/]" + path);
            WriteText(path, json);

            // convert and write XML if requested
            if (settings.IsXml)
            {
                // convert to XML
                json = "{\"root\":" + json + "}";
                XmlDocument? doc = JsonConvert.DeserializeXmlNode(json);
                string xml = doc?.OuterXml ?? "";
                path = Path.Combine(settings.OutputDir ?? "",
                    settings.Id + ".xml");
                Console.WriteLine("  -> " + path);
                WriteText(path, xml);
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

internal sealed class GetObjectCommandSettings : CommandSettings
{
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandArgument(1, "<ID>")]
    public string? Id { get; set; }

    [CommandArgument(2, "<OutputDirectory>")]
    [Description("The output directory")]
    public string? OutputDir { get; set; }

    [CommandOption("-g|--tag <RepositoryPluginTag>")]
    [Description("The repository factory plugin tag")]
    public string? RepositoryPluginTag { get; set; }

    [CommandOption("-p|--part")]
    [Description("The ID refers to a part rather than to an item")]
    public bool IsPart { get; set; }

    [CommandOption("-x|--xml")]
    [Description("Write also XML converted from JSON code")]
    public bool IsXml { get; set; }
}
