using Cadmus.Core.Storage;
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

internal sealed class DumpCommand : AsyncCommand<DumpCommandSettings>
{
    private static void ShowSettings(DumpCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[green]DUMP[/]");
        AnsiConsole.WriteLine($"Database: {settings.DatabaseName}");
        AnsiConsole.WriteLine($"Output directory: {settings.OutputDirectory}");
        if (settings.MaxItemsPerFile > 0)
            AnsiConsole.WriteLine($"Max items per file: {settings.MaxItemsPerFile}");
        AnsiConsole.WriteLine($"Incremental: {settings.IsIncremental}");
        AnsiConsole.WriteLine($"No part date: {settings.NoPartDate}");
        AnsiConsole.WriteLine($"No deleted: {settings.NoDeleted}");
        AnsiConsole.WriteLine($"No parts: {settings.NoParts}");
        AnsiConsole.WriteLine($"Indented: {settings.Indented}");

        if (settings.WhitePartTypeKeys is not null)
        {
            AnsiConsole.WriteLine(
                $"White part types: {string.Join(", ", settings.WhitePartTypeKeys)}");
        }
        if (settings.BlackPartTypeKeys is not null)
        {
            AnsiConsole.WriteLine(
                $"Black part types: {string.Join(", ", settings.BlackPartTypeKeys)}");
        }

        if (!string.IsNullOrEmpty(settings.UserId))
        {
            AnsiConsole.WriteLine($"User ID: {settings.UserId}");
        }
        if (settings.MinModified.HasValue)
        {
            AnsiConsole.WriteLine(
                $"Min modified: {settings.MinModified?.ToString("O")}");
        }
        if (settings.MaxModified.HasValue)
        {
            AnsiConsole.WriteLine(
                $"Max modified: {settings.MaxModified?.ToString("O")}");
        }

        if (!string.IsNullOrEmpty(settings.Title))
            AnsiConsole.WriteLine($"Title: {settings.Title}");

        if (!string.IsNullOrEmpty(settings.Description))
            AnsiConsole.WriteLine($"Description: {settings.Description}");

        if (!string.IsNullOrEmpty(settings.FacetId))
            AnsiConsole.WriteLine($"Facet ID: {settings.FacetId}");

        if (!string.IsNullOrEmpty(settings.GroupId))
            AnsiConsole.WriteLine($"Group ID: {settings.GroupId}");

        if (settings.Flags.HasValue)
        {
            AnsiConsole.WriteLine($"Flags: {settings.Flags.Value}");

            if (!string.IsNullOrEmpty(settings.FlagMatching))
                AnsiConsole.WriteLine($"Flag matching: {settings.FlagMatching}");
        }

        if (settings.PageSize > 0)
        {
            AnsiConsole.WriteLine($"Page number: {settings.PageNumber}");
            AnsiConsole.WriteLine($"Page size: {settings.PageSize}");
        }
    }

    private static FlagMatching ParseFlagMatching(string? value)
    {
        if (string.IsNullOrEmpty(value)) return 0;
        return Enum.Parse<FlagMatching>("Bits" + value, true);
    }

    protected override Task<int> ExecuteAsync(CommandContext context,
        DumpCommandSettings settings, CancellationToken cancel)
    {
        ShowSettings(settings);

        try
        {
            // get connection string
            string connectionString = string.Format(CultureInfo.InvariantCulture,
                ConfigurationService.Configuration!.GetConnectionString("Default")!,
                settings.DatabaseName);

            // create output directory if it does not exist
            if (!Directory.Exists(settings.OutputDirectory))
            {
                Directory.CreateDirectory(settings.OutputDirectory);
            }

            // create dumper
            CadmusMongoJsonDumper dumper = new(new CadmusJsonDumperOptions
            {
                ConnectionString = connectionString,
                DatabaseName = settings.DatabaseName,
                OutputDirectory = settings.OutputDirectory,
                MaxItemsPerFile = settings.MaxItemsPerFile,
                Indented = settings.Indented,
                IsIncremental = settings.IsIncremental,
                NoPartDate = settings.NoPartDate,
                NoDeleted = settings.NoDeleted,
                NoParts = settings.NoParts,
            });

            // dump
            AnsiConsole.WriteLine("Dumping data...");
            dumper.Dump(
                new CadmusDumpFilter
                {
                    WhitePartTypeKeys = settings.WhitePartTypeKeys != null
                        ? [..settings.WhitePartTypeKeys] : null,
                    BlackPartTypeKeys = settings.BlackPartTypeKeys != null
                        ? [..settings.BlackPartTypeKeys] : null,
                    Title = settings.Title,
                    Description = settings.Description,
                    FacetId = settings.FacetId,
                    GroupId = settings.GroupId,
                    Flags = settings.Flags,
                    FlagMatching = ParseFlagMatching(settings.FlagMatching),
                    UserId = settings.UserId,
                    MinModified = settings.MinModified,
                    MaxModified = settings.MaxModified,
                    PageNumber = settings.PageNumber,
                    PageSize = settings.PageSize,
                },
                CancellationToken.None,
                new Progress<ProgressReport>(p =>
                    AnsiConsole.MarkupLine($"  [yellow]{p.Count}[/]")));

            AnsiConsole.MarkupLine("[green]Done![/]");
            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: {ex.Message}[/]");
            return Task.FromResult(2);
        }
    }
}

public class DumpCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the name of the database to read items from.
    /// </summary>
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The source Cadmus database.")]
    public required string DatabaseName { get; set; }

    [CommandOption("-i|--incremental")]
    [Description("Incremental dump, " +
        "including only items/parts changed in the timeframe.")]
    public bool IsIncremental { get; set; }

    [CommandOption("--no-part-date")]
    [Description("Do not consider parts' date " +
        "when filtering items by time-based parameters.")]
    public bool NoPartDate { get; set; }

    [CommandOption("--no-deleted")]
    [Description("Do not include deleted items.")]
    public bool NoDeleted { get; set; }

    [CommandOption("--no-parts")]
    [Description("Do not include _parts in exported items.")]
    public bool NoParts { get; set; }

    [CommandOption("-o|--output-dir")]
    [Description("The output directory where dump files will be saved")]
    public string OutputDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath
            (Environment.SpecialFolder.DesktopDirectory),
        DateOnly.FromDateTime(DateTime.Now).ToString("yyyy-MM-dd"));

    [CommandOption("--max-file-items")]
    [Description("The maximum number of items to export per file. " +
        "If not specified (0), all items will be exported in a single file.")]
    [DefaultValue(0)]
    public int MaxItemsPerFile { get; set; }

    [CommandOption("--indented")]
    [Description("Indent JSON output.")]
    public bool Indented { get; set; }

    /// </summary>
    [CommandOption("-w|--white-part-types")]
    [Description("The keys of the part types to include in the export " +
        "(typeId[:roleId]). If not specified, all part types are included.")]
    public string[]? WhitePartTypeKeys { get; set; }

    [CommandOption("-b|--black-part-types")]
    [Description("The keys of the part types to exclude from the export " +
        "(typeId[:roleId]). If not specified, no part types are excluded.")]
    public string[]? BlackPartTypeKeys { get; set; }

    [CommandOption("-u|--user-id")]
    [Description("The user ID to filter items by. If not specified, " +
        "items by all users are included.")]
    public string? UserId { get; set; }

    [CommandOption("-n|--min-modified")]
    [Description("The minimum modified date and time filter.")]
    public DateTime? MinModified { get; set; }

    [CommandOption("-m|--max-modified")]
    [Description("The maximum modified date and time filter.")]
    public DateTime? MaxModified { get; set; }

    [CommandOption("-t|--title")]
    [Description("The item's title filter.")]
    public string? Title { get; set; }

    [CommandOption("--description")]
    [Description("The item's description filter.")]
    public string? Description { get; set; }

    [CommandOption("-f|--facet-id")]
    [Description("The item's facet ID filter.")]
    public string? FacetId { get; set; }

    [CommandOption("-g|--group-id")]
    [Description("The item's group ID filter.")]
    public string? GroupId { get; set; }

    [CommandOption("-l|--flags")]
    [Description("The bitwise item's flags filter.")]
    public int? Flags { get; set; }

    [CommandOption("--flag-matching")]
    [Description("The item's flags matching mode: " +
        "AllSet, AnySet, AllClear, AnyClear.")]
    public string? FlagMatching { get; set; }

    [CommandOption("-p|--page-number")]
    [Description("The page number to export if using paging.")]
    public int PageNumber { get; set; } = 1;

    [CommandOption("-s|--page-size")]
    [Description("The page size to export if using paging. " +
        "If not specified (0), all items are exported at once.")]
    public int PageSize { get; set; } = 0;
}
