using Cadmus.Index;
using Cadmus.Index.PgSql;
using Fusi.Tools.Data;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

/// <summary>
/// Build the SQL code for an index query, for diagnostic purposes.
/// </summary>
internal sealed class BuildIndexSqlCommand :
    AsyncCommand<BuildIndexSqlCommandSettings>
{
    private readonly PagingOptions _options;
    private readonly PgSqlQueryBuilder _builder;

    public BuildIndexSqlCommand()
    {
        _options = new PagingOptions();
        _builder = new PgSqlQueryBuilder();
    }

    private void Build(string query, bool pins)
    {
        try
        {
            PgSqlQuery sql = pins
                ? _builder.BuildPinQuery(query, _options)
                : _builder.BuildItemQuery(query, _options);
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(sql.ToString())}[/]");
        }
        catch (CadmusQueryException ex)
        {
            AnsiConsole.MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
        }
    }

    public override Task<int> ExecuteAsync(CommandContext context,
        BuildIndexSqlCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[green underline]BUILD INDEX SQL[/]");
        AnsiConsole.MarkupLine("Target: [cyan]" +
            (settings.Pins ? "pins" : "items") + "[/]");

        try
        {
            if (!string.IsNullOrEmpty(settings.Query))
            {
                AnsiConsole.MarkupLine(
                    $"Query: [cyan]{Markup.Escape(settings.Query)}[/]");
                Build(settings.Query, settings.Pins);
                return Task.FromResult(0);
            }

            while (true)
            {
                AnsiConsole.MarkupLine("Enter query: ");
                string? query = Console.ReadLine();
                if (string.IsNullOrEmpty(query) || query == "quit") break;
                Console.WriteLine();
                Build(query, settings.Pins);
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

public class BuildIndexSqlCommandSettings : CommandSettings
{
    [CommandOption("-q|--query <QUERY>")]
    [Description("The query text")]
    public string? Query { get; set; }

    [CommandOption("-p|--pins")]
    [Description("Build the SQL for searching pins rather than items")]
    public bool Pins { get; set; }
}
