using Cadmus.Migration.Cli.Commands;
using Serilog;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Migration.Cli;

/// <summary>
/// Main program.
/// </summary>
public static class Program
{
#if DEBUG
    private static void DeleteLogs()
    {
        foreach (var path in Directory.EnumerateFiles(
            AppDomain.CurrentDomain.BaseDirectory, "mig-log*.txt"))
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
        }
    }
#endif

    /// <summary>
    /// Entry point.
    /// </summary>
    /// <param name="args">The arguments.</param>
    public static async Task<int> Main(string[] args)
    {
        try
        {
#if DEBUG
            DeleteLogs();
#endif
            Console.OutputEncoding = Encoding.UTF8;
            Stopwatch stopwatch = new();
            stopwatch.Start();

            CommandApp app = new();
            app.Configure(config =>
            {
                config.AddCommand<RenderItemsCommand>("render")
                    .WithDescription("Render items");
                config.AddCommand<DumpCommand>("dump")
                    .WithDescription("Dump objects from a Cadmus database");
                config.AddCommand<DumpThesauriCommand>("dump-thesauri")
                    .WithDescription("Dump thesauri from a Cadmus database");
                config.AddCommand<ExportRdfCommand>("export-rdf")
                    .WithDescription("Export RDF data from a Cadmus graph database");
                config.AddCommand<JsonToXmlCommand>("json-to-xml")
                    .WithDescription("Convert item/part JSON to XML");
            });

            int result = await app.RunAsync(args);

            AnsiConsole.ResetColors();
            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                AnsiConsole.WriteLine("\nTime: {0}h{1}'{2}\"",
                    stopwatch.Elapsed.Hours,
                    stopwatch.Elapsed.Minutes,
                    stopwatch.Elapsed.Seconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            AnsiConsole.WriteException(ex);
            return 2;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }
}
