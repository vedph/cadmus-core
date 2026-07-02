using Serilog;
using Spectre.Console.Cli;
using Spectre.Console;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System;
using Cadmus.Cli.Commands;

namespace Cadmus.Cli;

public static class Program
{
#if DEBUG
    private static void DeleteLogs()
    {
        foreach (var path in Directory.EnumerateFiles(
            AppDomain.CurrentDomain.BaseDirectory, "cadmus-tool-log*.txt"))
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

    public static async Task<int> Main(string[] args)
    {
        try
        {
            // https://github.com/serilog/serilog-sinks-file
            string logFilePath = Path.Combine(
                Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location) ?? "",
                    "cadmus-tool-log.txt");
            Log.Logger = new LoggerConfiguration()
#if DEBUG
                .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
                .Enrich.FromLogContext()
                .WriteTo.File(logFilePath, rollingInterval: RollingInterval.Day)
                .CreateLogger();
#if DEBUG
            DeleteLogs();
#endif
            Stopwatch stopwatch = new();
            stopwatch.Start();

            CommandApp app = new();
            app.Configure(config =>
            {
                config.AddCommand<BuildIndexSqlCommand>("build-sql")
                    .WithDescription("Build SQL code from an index query.");

                config.AddCommand<CreateDatabaseCommand>("create-db")
                    .WithDescription("Create index/graph database.");

                config.AddCommand<GetObjectCommand>("get-obj")
                    .WithDescription("Get the item/part with the specified ID.");

                config.AddCommand<GraphUpdateClassesCommand>("graph-cls")
                    .WithDescription("Updates graph classes hierarchy.");

                config.AddCommand<GraphDerefMappingsCommand>("graph-deref")
                    .WithDescription("Dereferences graph mappings from a file " +
                    "into another one.");

                config.AddCommand<GraphImportCommand>("graph-import")
                    .WithDescription(
                    "Import nodes, triples, mappings or thesauri into graph.");

                config.AddCommand<GraphManyCommand>("graph-many")
                    .WithDescription("Map all the items into graph.");

                config.AddCommand<GraphOneCommand>("graph-one")
                    .WithDescription("Map a single item/part into graph.");

                config.AddCommand<IndexDatabaseCommand>("index")
                    .WithDescription("Build the database index.");

                config.AddCommand<SeedDatabaseCommand>("seed")
                    .WithDescription("Seed the database with mock items.");

                config.AddCommand<SeedUsersCommand>("seed-users")
                    .WithDescription("Seed user accounts into the auth database.");

                config.AddCommand<ThesaurusImportCommand>("thes-import")
                    .WithDescription("Import thesauri into the database.");

                config.AddCommand<RunMongoScriptCommand>("run-mongo")
                    .WithDescription("Run a MongoDB script.");
            });

            int result = await app.RunAsync(args);

            stopwatch.Stop();
            if (stopwatch.ElapsedMilliseconds > 1000)
            {
                Console.WriteLine("\nTime: {0}h{1}'{2}\"",
                    stopwatch.Elapsed.Hours,
                    stopwatch.Elapsed.Minutes,
                    stopwatch.Elapsed.Seconds);
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.Logger.Error(ex, ex.Message);
            Debug.WriteLine(ex.ToString());
            AnsiConsole.WriteException(ex);
            return 2;
        }
    }
}
