using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.Export;
using Cadmus.Export.Config;
using Cadmus.Export.ML;
using Cadmus.Migration.Cli.Services;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Migration.Cli.Commands;

/// <summary>
/// Render items via item composers.
/// </summary>
/// <seealso cref="ICommand" />
internal sealed class RenderItemsCommand : AsyncCommand<RenderItemsCommandSettings>
{
    private static void ShowSettings(RenderItemsCommandSettings settings)
    {
        AnsiConsole.MarkupLine("[green]RENDER[/]");
        AnsiConsole.WriteLine($"Database: {settings.DatabaseName}");
        AnsiConsole.WriteLine($"Config path: {settings.ConfigPath}");
        AnsiConsole.WriteLine("Factory provider tag: " +
            $"{settings.PreviewFactoryProviderTag ?? "-"}");
        AnsiConsole.WriteLine("Repository provider tag: " +
            $"{settings.RepositoryProviderTag ?? "-"}");
        AnsiConsole.WriteLine($"Composer key: {settings.ComposerKey}\n");
    }

    private static AppContextService GetContextService(string dbName)
    {
        ArgumentNullException.ThrowIfNull(dbName);

        return new AppContextService(
            new CadmusMigCliContextServiceConfig
            {
                ConnectionString = string.Format(CultureInfo.InvariantCulture,
                    ConfigurationService.Configuration!
                        .GetConnectionString("Default")!, dbName),
            });
    }

    protected override Task<int> ExecuteAsync(CommandContext context,
        RenderItemsCommandSettings settings, CancellationToken cancel)
    {
        ShowSettings(settings);

        try
        {
            string cs = string.Format(
                ConfigurationService.Configuration!.GetConnectionString("Default")!,
                settings.DatabaseName);

            AppContextService contextService =
                GetContextService(settings.DatabaseName);

            // load rendering config
            AnsiConsole.WriteLine("Loading rendering config...");
            string config = CommandHelper.LoadFileContent(settings.ConfigPath!);

            // get preview factory from its provider
            AnsiConsole.WriteLine("Building preview factory...");
            ICadmusRenderingFactoryProvider? provider =
                AppContextService.GetPreviewFactoryProvider(
                    settings.PreviewFactoryProviderTag);
            if (provider == null)
            {
                AnsiConsole.MarkupLine("[red]Preview factory provider not found[/]");
                return Task.FromResult(2);
            }
            CadmusRenderingFactory factory = provider.GetFactory(config,
                typeof(FSTeiOffItemComposer).Assembly);
            factory.ConnectionString = cs;

            // get the Cadmus repository from the specified plugin
            AnsiConsole.WriteLine("Building repository factory...");
            ICadmusRepository repository = contextService.GetCadmusRepository(
                settings.RepositoryProviderTag)
                ?? throw new InvalidOperationException(
                    "Unable to create Cadmus repository");

            // create the preview item composer
            AnsiConsole.WriteLine("Creating item composer...");
            IItemComposer? composer = factory.GetComposer(settings.ComposerKey);
            if (composer == null)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Could not find composer with key {settings.ComposerKey}.[/] " +
                    "Please check your rendering configuration.");
                return Task.FromResult(2);
            }

            // create ID collector
            AnsiConsole.WriteLine("Creating item collector...");
            IItemIdCollector? collector = factory.GetItemIdCollector();
            if (collector == null)
            {
                AnsiConsole.MarkupLine(
                    "[red]No item ID collector defined in configuration.[/]");
                return Task.FromResult(2);
            }

            // render items
            AnsiConsole.MarkupLine("[cyan]Rendering items...[/]");

            int n = 0;
            composer.Open();
            foreach (string id in collector.GetIds())
            {
                if (++n > settings.MaxItems && settings.MaxItems > 0) break;

                AnsiConsole.WriteLine($" - {n}: " + id);
                IItem? item = repository.GetItem(id, true);
                if (item != null)
                {
                    AnsiConsole.WriteLine("   " + item.Title);
                    composer.Compose(item);
                }
            }
            composer.Close();

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return Task.FromResult(2);
        }
    }
}

public class RenderItemsCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the name of the database to read items from.
    /// </summary>
    [CommandArgument(0, "<DatabaseName>")]
    [Description("The name of the source Cadmus database.")]
    public required string DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets the path to the rendering configuration file.
    /// </summary>
    [CommandArgument(1, "<ConfigPath>")]
    [Description("The path to the rendering configuration file.")]
    public required string ConfigPath { get; set; }

    /// <summary>
    /// Gets or sets the tag of the component found in some plugin and
    /// implementing <see cref="ICadmusRenderingFactoryProvider"/>.
    /// </summary>
    [CommandOption("--preview|-p")]
    [Description("The tag of the factory provider plugin for preview.")]
    public string? PreviewFactoryProviderTag { get; set; }

    /// <summary>
    /// Gets or sets the tag of the component found in some plugin and
    /// implementing Cadmus <see cref="IRepositoryProvider"/>.
    /// </summary>
    [CommandOption("--repository|-r")]
    [Description("The tag of the Cadmus repository provider plugin.")]
    public string? RepositoryProviderTag { get; set; }

    /// <summary>
    /// Gets or sets the key in the rendering configuration file for the
    /// item composer to use.
    /// </summary>
    [CommandOption("--composer|-c")]
    [Description("The key of the item composer to use (default='default').")]
    public string ComposerKey { get; set; } = "default";

    [CommandOption("--max|-m")]
    [Description("The maximum number of items to render (0=all).")]
    public int MaxItems { get; set; }
}
