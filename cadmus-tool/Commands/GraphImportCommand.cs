using Cadmus.Core.Config;
using Cadmus.Graph;
using Spectre.Console;
using Spectre.Console.Cli;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cadmus.Cli.Commands;

/// <summary>
/// Import graph presets command.
/// </summary>
/// <seealso cref="ICommand" />
internal sealed class GraphImportCommand : AsyncCommand<GraphImportCommandSettings>
{
    private static void ImportMappings(Stream source, IGraphRepository repository,
        GraphImportCommandSettings settings)
    {
        // source id : graph id
        Dictionary<int, int> ids = [];

        JsonGraphPresetReader reader = new();
        foreach (NodeMapping mapping in reader.LoadMappings(source))
        {
            Console.WriteLine(mapping);
            if (!settings.IsDry)
            {
                // adjust IDs
                int sourceId = mapping.Id;
                mapping.Id = 0;
                if (mapping.ParentId != 0)
                    mapping.ParentId = ids[mapping.ParentId];
                repository.AddMapping(mapping);
                ids[sourceId] = mapping.Id;
            }
        }
    }

    private static void ImportNodes(Stream source, IGraphRepository repository,
        GraphImportCommandSettings settings)
    {
        JsonGraphPresetReader reader = new();

        foreach (UriNode node in reader.ReadNodes(source))
        {
            if (!settings.IsDry)
            {
                node.Id = repository.AddUri(node.Uri!);
                Console.WriteLine(node);
                repository.AddNode(node);
            }
            else
            {
                Console.WriteLine(node);
            }
        }
    }

    private static void HydrateTriple(UriTriple triple,
        IGraphRepository repository)
    {
        // subject
        if (triple.SubjectId == 0)
        {
            if (triple.SubjectUri == null)
                throw new ArgumentNullException("No subject for triple: " + triple);
            triple.SubjectId = repository.LookupId(triple.SubjectUri);
            if (triple.SubjectId == 0)
                throw new ArgumentNullException("Missing URI " + triple.SubjectUri);
        }

        // predicate
        if (triple.PredicateId == 0)
        {
            if (triple.PredicateUri == null)
                throw new ArgumentNullException("No predicate for triple: " + triple);
            triple.PredicateId = repository.LookupId(triple.PredicateUri);
            if (triple.PredicateId == 0)
                throw new ArgumentNullException("Missing URI " + triple.PredicateUri);
        }

        // object
        if (triple.ObjectLiteral == null &&
            (triple.ObjectId == 0 || triple.ObjectId == null))
        {
            if (triple.ObjectUri == null)
                throw new ArgumentNullException("No object for triple: " + triple);
            triple.ObjectId = repository.LookupId(triple.ObjectUri);
            if (triple.ObjectId == 0)
                throw new ArgumentNullException("Missing URI " + triple.ObjectUri);
        }
    }

    private static void ImportTriples(Stream source, IGraphRepository repository,
        GraphImportCommandSettings settings)
    {
        JsonGraphPresetReader reader = new();
        foreach (UriTriple triple in reader.ReadTriples(source))
        {
            HydrateTriple(triple, repository);
            Console.WriteLine(triple);
            if (!settings.IsDry) repository.AddTriple(triple);
            else Console.WriteLine(triple);
        }
    }

    private static void ImportThesauri(Stream source, IGraphRepository repository,
        GraphImportCommandSettings settings)
    {
        string json = new StreamReader(source, Encoding.UTF8).ReadToEnd();
        Thesaurus[] thesauri = JsonSerializer.Deserialize<Thesaurus[]>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            })!;
        foreach (Thesaurus thesaurus in thesauri)
        {
            Console.WriteLine(thesaurus);
            if (!settings.IsDry)
            {
                repository.AddThesaurus(thesaurus,
                    settings.ThesaurusIdAsRoot,
                    settings.ThesaurusIdPrefix);
            }
        }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context,
        GraphImportCommandSettings settings, CancellationToken cancel)
    {
        AnsiConsole.MarkupLine("[red underline]IMPORT INTO GRAPH[/]");
        AnsiConsole.MarkupLine($"Source: [cyan]{settings.SourcePath}[/]");
        AnsiConsole.MarkupLine($"Mode: [cyan]{settings.Mode}[/]");
        AnsiConsole.MarkupLine($"Database: [cyan]{settings.DatabaseName}[/]");
        if (!string.IsNullOrEmpty(settings.RepositoryPluginTag))
        {
            AnsiConsole.MarkupLine(
                $"Repository plugin tag: [cyan]{settings.RepositoryPluginTag}[/]");
        }
        AnsiConsole.MarkupLine(
            $"Dry mode: [cyan]{(settings.IsDry ? "yes" : "no")}[/]");

        if (char.ToLowerInvariant(settings.Mode) == 'h')
        {
            AnsiConsole.MarkupLine("Thesaurus ID as root: [cyan]" +
                $"{(settings.ThesaurusIdAsRoot ? "yes" : "no")}[/]");
            AnsiConsole.MarkupLine("Thesaurus ID prefix: " +
                $"[cyan]{settings.ThesaurusIdPrefix}[/]");
        }

        try
        {
            IGraphRepository repository = GraphHelper.GetGraphRepository(
                settings.DatabaseName!);
            if (repository != null)
            {
                await using Stream source = new FileStream(settings.SourcePath!,
                    FileMode.Open, FileAccess.Read, FileShare.Read);

                switch (char.ToLowerInvariant(settings.Mode))
                {
                    case 'm':
                        ImportMappings(source, repository, settings);
                        break;
                    case 't':
                        ImportTriples(source, repository, settings);
                        break;
                    case 'h':
                        ImportThesauri(source, repository, settings);
                        break;
                    default:
                        ImportNodes(source, repository, settings);
                        break;
                }
            }

            return await Task.FromResult(0);
        }
        catch (Exception ex)
        {
            CliHelper.DisplayException(ex);
            return await Task.FromResult(2);
        }
    }
}

/// <summary>
/// Options for <see cref="GraphImportCommand"/>
/// </summary>
internal class GraphImportCommandSettings : CommandSettings
{
    [CommandArgument(0, "<SourcePath>")]
    [Description("The path to the source file")]
    public string? SourcePath { get; set; }

    [CommandArgument(1, "<DatabaseName>")]
    [Description("The database name")]
    public string? DatabaseName { get; set; }

    [CommandOption("-g|--tag <RepositoryPluginTag>")]
    [Description("The repository factory plugin tag")]
    public string? RepositoryPluginTag { get; set; }

    [CommandOption("-m|--mode <ImportMode>")]
    [DefaultValue('n')]
    [Description("Import mode: (n)odes (t)riples (m)appings t(h)esauri")]
    public char Mode { get; set; }

    [CommandOption("-d|--dry")]
    [DefaultValue(false)]
    [Description("Dry mode - don't write to database")]
    public bool IsDry { get; set; }

    [CommandOption("-r|--root")]
    [DefaultValue(false)]
    [Description("True to import the thesaurus' ID as the root class node")]
    public bool ThesaurusIdAsRoot { get; set; }

    [CommandOption("-p|--prefix <ThesaurusIdPrefix>")]
    [Description("The prefix to add to each thesaurus' class node")]
    public string? ThesaurusIdPrefix { get; set; }

    public GraphImportCommandSettings()
    {
        Mode = 'n';
    }
}
