using Cadmus.Cli.Models;
using Cadmus.Cli.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

/// <summary>
/// Command to seed user accounts into the authentication database.
/// </summary>
internal sealed class SeedUsersCommand : AsyncCommand<SeedUsersCommandSettings>
{
    /// <summary>
    /// Builds the PostgreSQL connection string for the auth database.
    /// </summary>
    /// <param name="databaseName">The database name.</param>
    /// <returns>The connection string.</returns>
    private static string BuildConnectionString(string databaseName)
    {
        string? template = CliAppContext.Configuration
            .GetConnectionString("Auth");

        if (string.IsNullOrEmpty(template))
        {
            throw new InvalidOperationException(
                "Auth connection string not found in configuration. " +
                "Ensure appsettings.json contains ConnectionStrings:Auth.");
        }

        return string.Format(CultureInfo.InvariantCulture, template, databaseName);
    }

    /// <summary>
    /// Creates an AuthDbContext for the specified connection string.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <returns>The database context.</returns>
    private static AuthDbContext CreateDbContext(string connectionString)
    {
        DbContextOptionsBuilder<AuthDbContext> builder = new();
        builder.UseNpgsql(connectionString);

        return new AuthDbContext(builder.Options);
    }

    /// <summary>
    /// Displays the users loaded from the JSON file.
    /// </summary>
    /// <param name="users">The users to display.</param>
    private static void DisplayUsers(IReadOnlyList<NamedSeededUserOptions> users)
    {
        Table table = new();
        table.AddColumn("Username");
        table.AddColumn("Email");
        table.AddColumn("Name");
        table.AddColumn("Roles");

        foreach (NamedSeededUserOptions user in users)
        {
            string name = $"{user.FirstName} {user.LastName}".Trim();
            string roles = user.Roles != null
                ? string.Join(", ", user.Roles)
                : "";

            table.AddRow(
                user.UserName ?? "",
                user.Email ?? "",
                name,
                roles);
        }

        AnsiConsole.Write(table);
    }

    /// <summary>
    /// Executes the seed users command.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>0 on success, 1 on warning, 2 on error.</returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        SeedUsersCommandSettings settings,
        CancellationToken cancellationToken)
    {
        // Display command header
        AnsiConsole.MarkupLine("[red underline]SEED USERS[/]");
        AnsiConsole.MarkupLine($"JSON file: [cyan]{settings.JsonFilePath}[/]");
        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        AnsiConsole.MarkupLine($"Dry run: [cyan]{settings.IsDryRun}[/]");
        AnsiConsole.WriteLine();

        // Log to Serilog
        Serilog.Log.Information(
            "SEED USERS: JsonFile={JsonFilePath}, Database={DatabaseName}, " +
            "DryRun={IsDryRun}",
            settings.JsonFilePath,
            settings.DatabaseName,
            settings.IsDryRun);

        try
        {
            // Validate file path
            if (!File.Exists(settings.JsonFilePath))
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error: File not found: {settings.JsonFilePath}[/]");
                return 2;
            }

            // Load users from JSON
            AnsiConsole.MarkupLine("[yellow]Loading users from JSON...[/]");
            NamedSeededUserOptions[] users;

            try
            {
                users = UserSeeder.LoadUsersFromJson(settings.JsonFilePath!);
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine(
                    $"[red]Error loading JSON: {ex.Message}[/]");
                Serilog.Log.Error(ex, "Error loading users JSON");
                return 2;
            }

            AnsiConsole.MarkupLine($"Loaded [green]{users.Length}[/] user(s).");
            AnsiConsole.WriteLine();

            // Display loaded users
            DisplayUsers(users);
            AnsiConsole.WriteLine();

            // Validate users
            AnsiConsole.MarkupLine("[yellow]Validating users...[/]");
            List<string> validationErrors = UserSeeder.ValidateUsers(users);

            if (validationErrors.Count > 0)
            {
                AnsiConsole.MarkupLine("[red]Validation errors:[/]");
                foreach (string error in validationErrors)
                {
                    AnsiConsole.MarkupLine($"  [red]• {error}[/]");
                }
                return 2;
            }

            AnsiConsole.MarkupLine("[green]Validation passed.[/]");
            AnsiConsole.WriteLine();

            // If dry run, stop here
            if (settings.IsDryRun)
            {
                AnsiConsole.MarkupLine(
                    "[yellow]Dry run completed. " +
                    "No changes were made to the database.[/]");
                return 0;
            }

            // Build connection string
            string connectionString = BuildConnectionString(settings.DatabaseName!);
            Serilog.Log.Debug("Connection string built for database {Database}",
                settings.DatabaseName);

            // Seed users
            UserSeedResult result;

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .StartAsync("Seeding users...", async ctx =>
                {
                    ctx.Status("Connecting to database...");

                    using AuthDbContext dbContext = CreateDbContext(connectionString);

                    // Test connection
                    try
                    {
                        bool canConnect = await dbContext.Database.CanConnectAsync(
                            cancellationToken);
                        if (!canConnect)
                        {
                            throw new InvalidOperationException(
                                "Cannot connect to the database. " +
                                "Verify the connection string and database name.");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException(
                            $"Database connection failed: {ex.Message}", ex);
                    }

                    ctx.Status("Seeding users...");

                    UserSeeder seeder = new(dbContext, message =>
                    {
                        // Log seeder messages
                        AnsiConsole.MarkupLine(message);
                        Serilog.Log.Information(message);
                    });

                    result = await seeder.SeedAsync(users);

                    // Display results
                    AnsiConsole.WriteLine();
                    AnsiConsole.MarkupLine("[green]Seeding completed![/]");
                    AnsiConsole.WriteLine();

                    // Results summary
                    Table resultsTable = new();
                    resultsTable.AddColumn("Metric");
                    resultsTable.AddColumn("Count");

                    resultsTable.AddRow("Users processed",
                        result.UsersProcessed.ToString());
                    resultsTable.AddRow("Users created",
                        $"[green]{result.UsersCreated}[/]");
                    resultsTable.AddRow("Users updated",
                        $"[yellow]{result.UsersUpdated}[/]");
                    resultsTable.AddRow("Roles created",
                        $"[green]{result.RolesCreated}[/]");
                    resultsTable.AddRow("Role assignments added",
                        $"[green]{result.RoleAssignmentsAdded}[/]");

                    if (result.Errors.Count > 0)
                    {
                        resultsTable.AddRow("Errors",
                            $"[red]{result.Errors.Count}[/]");
                    }

                    AnsiConsole.Write(resultsTable);

                    // Display errors if any
                    if (result.Errors.Count > 0)
                    {
                        AnsiConsole.WriteLine();
                        AnsiConsole.MarkupLine("[red]Errors occurred:[/]");
                        foreach (string error in result.Errors)
                        {
                            AnsiConsole.MarkupLine($"  [red]• {error}[/]");
                            Serilog.Log.Error(error);
                        }
                    }
                });

            Serilog.Log.Information("SEED USERS completed successfully");
            return 0;
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "SEED USERS failed: {Message}", ex.Message);
            CliHelper.DisplayException(ex);
            return 2;
        }
    }
}

/// <summary>
/// Settings for the <see cref="SeedUsersCommand"/>.
/// </summary>
internal sealed class SeedUsersCommandSettings : CommandSettings
{
    /// <summary>
    /// Gets or sets the path to the JSON file containing the users to seed.
    /// </summary>
    [CommandArgument(0, "<JsonFilePath>")]
    [Description("The path to the JSON file containing users to seed")]
    public string? JsonFilePath { get; set; }

    /// <summary>
    /// Gets or sets the database name.
    /// </summary>
    [CommandArgument(1, "<DatabaseName>")]
    [Description("The authentication database name")]
    public string? DatabaseName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this is a dry run.
    /// When true, users are loaded and validated but not written to the database.
    /// </summary>
    [CommandOption("-d|--dry")]
    [Description("Dry run: load and validate users without writing to database")]
    public bool IsDryRun { get; set; }
}
