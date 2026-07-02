using Cadmus.Cli.Services;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.Import;
using Cadmus.Import.Excel;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

public sealed class ThesaurusImportCommand :
    AsyncCommand<ThesaurusImportCommandSettings>
{
    private static ImportUpdateMode GetMode(char c)
    {
        // return ImportUpdateMode enum according to c R P S
        return char.ToUpperInvariant(c) switch
        {
            'R' => ImportUpdateMode.Replace,
            'P' => ImportUpdateMode.Patch,
            'S' => ImportUpdateMode.Synch,
            _ => throw new ArgumentException("Invalid mode", nameof(c)),
        };
    }

    protected override Task<int> ExecuteAsync(
        CommandContext context, ThesaurusImportCommandSettings settings,
        CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]IMPORT THESAURI[/]");
        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine($"Input: [cyan]{settings.InputFileMask}[/]");
        AnsiConsole.MarkupLine($"Mode: [cyan]{settings.Mode}[/]");
        AnsiConsole.MarkupLine(
            $"Dry run: [cyan]{(settings.IsDryRun ? "yes" : "no")}[/]");
        if (settings.ExcelSheet != 1)
            AnsiConsole.MarkupLine($"Excel sheet: [cyan]{settings.ExcelSheet}[/]");
        if (settings.ExcelRow != 1)
            AnsiConsole.MarkupLine($"Excel row: [cyan]{settings.ExcelRow}[/]");
        if (settings.ExcelColumn != 1)
            AnsiConsole.MarkupLine($"Excel column: [cyan]{settings.ExcelColumn}[/]");

        try
        {
            // repository
            AnsiConsole.MarkupLine("Creating repository...");
            string cs = string.Format(
                CliAppContext.Configuration.GetConnectionString("Mongo")!,
                settings.DatabaseName);
            ICadmusRepository repository = CliHelper.GetCadmusRepository(null, cs);

            ExcelThesaurusReaderOptions xlsOptions = new()
            {
                SheetIndex = settings.ExcelSheet - 1,
                RowOffset = settings.ExcelRow - 1,
                ColumnOffset = settings.ExcelColumn - 1
            };

            // import
            int n = 0;
            foreach (string path in Directory.EnumerateFiles(
                Path.GetDirectoryName(settings.InputFileMask) ?? "",
                Path.GetFileName(settings.InputFileMask)!)
                .OrderBy(s => s))
            {
                // load thesaurus
                AnsiConsole.MarkupLine($"[yellow]{++n:000}[/] [green]{path}[/]");

                using Stream stream = new FileStream(path, FileMode.Open,
                    FileAccess.Read, FileShare.Read);
                using IThesaurusReader reader =
                    Path.GetExtension(path).ToLowerInvariant() switch
                    {
                        ".csv" => new CsvThesaurusReader(stream),
                        ".xls" => new ExcelThesaurusReader(stream, xlsOptions),
                        ".xlsx" => new ExcelThesaurusReader(stream),
                        _ => new JsonThesaurusReader(stream)
                    };

                while (reader.Next())
                {
                    Thesaurus source = reader.Current!;
                    AnsiConsole.MarkupLine($"  [cyan]{source}[/]");

                    // fetch from repository
                    Thesaurus? target = repository.GetThesaurus(source.Id);

                    // import
                    Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
                        GetMode(settings.Mode));

                    // save
                    if (!settings.IsDryRun) repository.AddThesaurus(result);
                }
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

public class ThesaurusImportCommandSettings : CommandSettings
{
    [CommandArgument(0, "<InputFileMask>")]
    [Description("The input files mask")]
    public string? InputFileMask { get; set; }

    [CommandArgument(1, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandOption("-m|--mode <Mode>")]
    [Description("The import mode: R=replace, P=patch, S=synch")]
    [DefaultValue('R')]
    public char Mode { get; set; }

    [CommandOption("-d|--dry")]
    [Description("Dry run")]
    public bool IsDryRun { get; set; }

    [CommandOption("-s|--sheet <Number>")]
    [Description("The ordinal number of the sheet to read data from in an Excel file")]
    [DefaultValue(1)]
    public int ExcelSheet { get; set; }

    [CommandOption("-r|--row <Number>")]
    [Description("The first row number to read data from in an Excel sheet")]
    [DefaultValue(1)]
    public int ExcelRow { get; set; }

    [CommandOption("-c|--col <Number>")]
    [Description("The first column number to read data from in an Excel sheet")]
    [DefaultValue(1)]
    public int ExcelColumn { get; set; }

    public ThesaurusImportCommandSettings()
    {
        Mode = 'R';
        ExcelSheet = 1;
        ExcelRow = 1;
        ExcelColumn = 1;
    }
}