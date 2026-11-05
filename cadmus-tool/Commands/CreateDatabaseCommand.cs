using Cadmus.Cli.Services;
using Cadmus.Graph.Ef.PgSql;
using Cadmus.Index.PgSql;
using System.Globalization;
using Fusi.DbManager.PgSql;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

/// <summary>
/// Create an index or graph PostgreSQL database with its schema, unless
/// it already exists. The connection string template (with placeholder
/// <c>{0}</c> for the database name) is <c>ConnectionStrings:Index</c> for
/// the index, and <c>ConnectionStrings:Graph</c> (falling back to
/// <c>ConnectionStrings:Index</c>, like the API) for the graph. Both can be
/// overridden via environment variables (e.g. <c>ConnectionStrings__Graph</c>).
/// </summary>
internal sealed class CreateDatabaseCommand :
    AsyncCommand<CreateDatabaseCommandSettings>
{
    private static string GetTemplate(string name, string? fallbackName = null)
    {
        string? cst = CliAppContext.Configuration.GetConnectionString(name);
        if (string.IsNullOrEmpty(cst) && fallbackName != null)
            cst = CliAppContext.Configuration.GetConnectionString(fallbackName);
        if (string.IsNullOrEmpty(cst))
        {
            throw new InvalidOperationException(
                $"Missing connection string ConnectionStrings:{name}");
        }
        return cst;
    }

    private static string GetServerInfo(string template)
    {
        // never display the password
        NpgsqlConnectionStringBuilder builder = new(
            PgSqlDbManager.GetConnectionString(template, null));
        return $"{builder.Host}:{builder.Port} (user {builder.Username})";
    }

    /// <summary>
    /// Creates the specified database with its schema, unless it exists.
    /// If its schema fails, the database is removed by the manager, so that
    /// it does not get left without its schema.
    /// </summary>
    /// <param name="template">The connection string template.</param>
    /// <param name="name">The database name.</param>
    /// <param name="schema">The schema SQL code.</param>
    /// <returns>True if created, false if already existing.</returns>
    private static bool CreateDatabase(string template, string name,
        string schema)
    {
        PgSqlDbManager manager = new(template);
        if (manager.Exists(name)) return false;

        manager.CreateDatabase(name, schema, null);
        return true;
    }

    private static async Task<int> CreateIndexDatabaseAsync(string name,
        CancellationToken cancel)
    {
        string template = GetTemplate("Index");
        AnsiConsole.MarkupLine("Server: [cyan]" +
            $"{Markup.Escape(GetServerInfo(template))}[/]");

        bool created = false;
        await AnsiConsole.Status().StartAsync("Creating database...",
            async _ =>
            {
                // this also updates the schema of an existing database
                created = await PgSqlIndexSchema.EnsureDatabaseAsync(
                    string.Format(CultureInfo.InvariantCulture, template, name),
                    null, cancel);
            });

        AnsiConsole.MarkupLine(created
            ? "[green]Database created.[/]"
            : "[yellow]Database already exists: schema updated.[/]");
        return 0;
    }

    public override async Task<int> ExecuteAsync(CommandContext context,
       CreateDatabaseCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]CREATE DATABASE[/]");
        AnsiConsole.MarkupLine("Database role: [cyan]" +
            $"{Markup.Escape(settings.DatabaseRole)}[/]");
        AnsiConsole.MarkupLine("Database name: [cyan]" +
            $"{Markup.Escape(settings.DatabaseName)}[/]");

        if (string.IsNullOrWhiteSpace(settings.DatabaseName))
        {
            AnsiConsole.MarkupLine("[red]Database name not specified[/]");
            return 1;
        }

        try
        {
            string template;
            string schema;
            switch (settings.DatabaseRole.ToLowerInvariant())
            {
                case "index":
                    return await CreateIndexDatabaseAsync(
                        settings.DatabaseName, cancel);
                case "graph":
                    template = GetTemplate("Graph", "Index");
                    schema = EfPgSqlGraphRepository.GetSchema();
                    break;
                default:
                    AnsiConsole.MarkupLine("[red]Invalid database role[/] " +
                        "(expected index or graph): " +
                        Markup.Escape(settings.DatabaseRole));
                    return 1;
            }

            AnsiConsole.MarkupLine("Server: [cyan]" +
                $"{Markup.Escape(GetServerInfo(template))}[/]");

            bool created = false;
            AnsiConsole.Status().Start("Creating database...", _ =>
            {
                created = CreateDatabase(template, settings.DatabaseName,
                    schema);
            });

            AnsiConsole.MarkupLine(created
                ? "[green]Database created.[/]"
                : "[yellow]Database already exists: nothing done.[/]");
            return 0;
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return 2;
        }
    }
}

public class CreateDatabaseCommandSettings: CommandSettings
{
    [CommandArgument(0, "<DatabaseRole>")]
    [Description("The database role: index or graph")]
    public string DatabaseRole { get; set; }

    [CommandArgument(1, "<DatabaseName>")]
    [Description("The database name (for the API's graph, this is " +
        "DatabaseNames:Data plus -graph, e.g. cadmus-graph)")]
    public string DatabaseName { get; set; }

    public CreateDatabaseCommandSettings()
    {
        DatabaseRole = "index";
        DatabaseName = "";
    }
}
