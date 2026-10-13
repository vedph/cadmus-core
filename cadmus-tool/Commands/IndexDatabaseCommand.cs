using Cadmus.Cli.Services;
using Cadmus.Core.Storage;
using Cadmus.Index;
using Cadmus.Index.Config;
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

namespace Cadmus.Cli.Commands;

internal sealed class IndexDatabaseCommand :
    AsyncCommand<IndexDatabaseCommandSettings>
{
    private static string LoadProfile(string path)
    {
        using StreamReader reader = File.OpenText(path);
        return reader.ReadToEnd();
    }

    public async override Task<int> ExecuteAsync(CommandContext context,
        IndexDatabaseCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]INDEX DATABASE[/]");
        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine($"Profile file: [cyan]{settings.ProfilePath}[/]");
        if (!string.IsNullOrEmpty(settings.RepositoryPluginTag))
        {
            AnsiConsole.MarkupLine(
                $"Repository plugin tag: [cyan]{settings.RepositoryPluginTag}[/]");
        }
        AnsiConsole.MarkupLine($"Clear: [cyan]{settings.ClearDatabase}[/]\n");

        Serilog.Log.Information("INDEX DATABASE: " +
                     "Database: {DatabaseName}, " +
                     "Profile file: {ProfilePath}, " +
                     "Repository plugin tag: {RepositoryPluginTag}\n" +
                     "Clear: {settings.ClearDatabase}",
                     settings.DatabaseName,
                     settings.ProfilePath,
                     settings.RepositoryPluginTag,
                     settings.ClearDatabase);

        try
        {
            string profileContent = LoadProfile(settings.ProfilePath!);

            string cs = string.Format(
                CliAppContext.Configuration.GetConnectionString("Index")!,
                settings.DatabaseName);

            StandardItemIndexFactoryProvider provider = new(cs);

            ItemIndexFactory factory = provider.GetFactory(profileContent);
            IItemIndexWriter? writer = factory.GetItemIndexWriter()
                ?? throw new InvalidOperationException(
                    "Unable to instantiate item index writer");

            // repository
            AnsiConsole.WriteLine("Creating repository...");
            ICadmusRepository repository = CliHelper.GetCadmusRepository(
                settings.RepositoryPluginTag,
                string.Format(CultureInfo.InvariantCulture,
                    CliAppContext.Configuration.GetConnectionString("Mongo")!,
                    settings.DatabaseName));

            // index
            AnsiConsole.WriteLine("Ensuring that index is created...");
            await writer.CreateIndex();

            await AnsiConsole.Progress().StartAsync(async ctx =>
                {
                    ProgressTask task = ctx.AddTask("Indexing database");
                    ItemIndexer indexer = new(writer)
                    {
                        Logger = CliAppContext.Logger
                    };
                    if (settings.ClearDatabase) await indexer.Clear();

                    indexer.Build(repository, new ItemFilter(),
                        CancellationToken.None,
                        new Progress<ProgressReport>(
                            r => task.Increment(r.Percent - task.Value)));

                    task.Increment(100 - task.Value);
                });
            writer.Close();
            return 0;
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return 2;
        }
    }
}

internal class IndexDatabaseCommandSettings : CommandSettings
{
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandArgument(1, "<JsonProfilePath>")]
    [Description("The indexer profile JSON file path")]
    public string? ProfilePath { get; set; }

    [CommandOption("-g|--tag <RepositoryPluginTag>")]
    [Description("The repository factory plugin tag")]
    public string? RepositoryPluginTag { get; set; }

    [CommandOption("-c|--clear")]
    [Description("Clear before indexing")]
    public bool ClearDatabase { get; set; }
}
