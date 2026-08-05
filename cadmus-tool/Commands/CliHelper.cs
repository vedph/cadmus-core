using Cadmus.Cli.Core;
using Cadmus.Cli.Services;
using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.Seed;
using Spectre.Console;
using System;
using System.IO;
using System.Text;

namespace Cadmus.Cli.Commands;

internal static class CliHelper
{
    public static string LoadFileContent(string path)
    {
        using StreamReader reader = new(path, Encoding.UTF8);
        return reader.ReadToEnd();
    }

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
