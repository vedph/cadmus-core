using Cadmus.Cli.Services;
using Cadmus.Index.Ef.PgSql;
using Fusi.DbManager.PgSql;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

internal sealed class CreateDatabaseCommand :
    AsyncCommand<CreateDatabaseCommandSettings>
{
    public static void CreateIndexDatabase(string dbName)
    {
        string cst = CliAppContext.Configuration.GetConnectionString("Index")!;

        PgSqlDbManager mgr = new(cst);
        mgr.CreateDatabase(dbName, EfPgSqlItemIndexWriter.GetPgSqlSchema(), null);
    }

    public override Task<int> ExecuteAsync(CommandContext context,
       CreateDatabaseCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]CREATE DATABASE[/]");
        AnsiConsole.MarkupLine($"Database role: [cyan]{settings.DatabaseRole}[/]");
        AnsiConsole.MarkupLine($"Database name: [cyan]{settings.DatabaseName}[/]");

        try
        {
            int result = 0;
            AnsiConsole.Status().Start("Preparing...", _ =>
            {
                switch (settings.DatabaseRole.ToLowerInvariant())
                {
                    case "index":
                        AnsiConsole.MarkupLine(
                            $"Creating index {settings.DatabaseName}...");
                        CreateIndexDatabase(settings.DatabaseName);
                        break;
                    case "graph":
                        AnsiConsole.MarkupLine(
                            $"Creating graph {settings.DatabaseName}...");
                        GraphHelper.CreateGraphDatabase(settings.DatabaseName);
                        break;
                    default:
                        AnsiConsole.MarkupLine("[red]Invalid database role[/]: " +
                            settings.DatabaseRole);
                        result = 1;
                        break;
                }
            });

            if (result == 0) AnsiConsole.MarkupLine("[green]Completed.[/]");

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return Task.FromResult(2);
        }
    }
}

public class CreateDatabaseCommandSettings: CommandSettings
{
    [CommandArgument(0, "<DatabaseRole>")]
    [Description("The database role: index or graph")]
    public string DatabaseRole { get; set; }

    [CommandArgument(1, "<DatabaseName>")]
    [Description("The database name")]
    public string DatabaseName { get; set; }

    public CreateDatabaseCommandSettings()
    {
        DatabaseRole = "index";
        DatabaseName = "";
    }
}
