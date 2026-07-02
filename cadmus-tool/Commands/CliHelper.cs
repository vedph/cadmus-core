using Cadmus.Cli.Core;
using Cadmus.Core.Storage;
using System.IO;
using Cadmus.Core;
using Cadmus.Cli.Services;
using Cadmus.Seed;
using Spectre.Console;
using System;

namespace Cadmus.Cli.Commands;

internal static class CliHelper
{
    public static ICadmusRepository GetCadmusRepository(string? tag,
        string connStr)
    {
        IRepositoryProvider? provider =
            (string.IsNullOrEmpty(tag)
            ? new StandardRepositoryProvider()
            : PluginFactoryProvider.GetFromTag<IRepositoryProvider>(tag))
            ?? throw new FileNotFoundException(
                "The requested repository provider tag " +
                tag +
                " was not found among plugins in " +
                PluginFactoryProvider.GetPluginsDir());

        provider.ConnectionString = connStr;
        return provider.CreateRepository();
    }

    public static IPartSeederFactoryProvider GetSeederFactoryProvider(string? tag)
    {
        return string.IsNullOrEmpty(tag)
            ? new StandardPartSeederFactoryProvider()
            : PluginFactoryProvider.GetFromTag<IPartSeederFactoryProvider>(tag)
                ?? throw new FileNotFoundException(
                    "The requested part seeders provider tag " +
                    tag +
                    " was not found among plugins in " +
                    PluginFactoryProvider.GetPluginsDir());
    }

    public static void DisplayException(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        AnsiConsole.MarkupLineInterpolated($"[red]{ex.Message}[/]");
        Exception? inner = ex.InnerException;
        while (inner != null)
        {
            AnsiConsole.MarkupLineInterpolated($"- [red]{inner.Message}[/]");
            inner = inner.InnerException;
        }
        AnsiConsole.MarkupLineInterpolated($"[yellow]{ex.StackTrace}[/]");
    }
}
