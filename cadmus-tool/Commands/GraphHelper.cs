using Cadmus.Cli.Services;
using Cadmus.Graph;
using Cadmus.Graph.Ef;
using Cadmus.Graph.Ef.PgSql;
using Fusi.DbManager.PgSql;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Cadmus.Cli.Commands;

internal static class GraphHelper
{
    public static string LoadText(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        using StreamReader reader = File.OpenText(path);
        return reader.ReadToEnd();
    }

    public static IList<NodeMapping> ParseMappings(string json)
    {
        List<NodeMapping> mappings = [];
        JsonSerializerOptions options = new()
        {
            AllowTrailingCommas = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new NodeMappingOutputJsonConverter());

        mappings.AddRange(JsonSerializer.Deserialize<IList<NodeMapping>>(
            json ?? "{}",
            options) ?? Array.Empty<NodeMapping>());

        return mappings;
    }

    public static IList<NodeMapping> LoadMappings(string path)
    {
        return ParseMappings(LoadText(path));
    }

    public static void CreateGraphDatabase(string dbName)
    {
        ArgumentNullException.ThrowIfNull(dbName);

        string cst = CliAppContext.Configuration.GetConnectionString("Graph")!;
        PgSqlDbManager mgr = new(cst);
        mgr.CreateDatabase(dbName, EfPgSqlGraphRepository.GetSchema(), null);
    }

    public static IGraphRepository GetGraphRepository(string dbName)
    {
        ArgumentNullException.ThrowIfNull(dbName);

        string cs = string.Format(CliAppContext.Configuration
            .GetConnectionString("Graph")!, dbName);

        EfGraphRepository repository = new EfPgSqlGraphRepository();
        repository.Configure(new EfGraphRepositoryOptions
        {
            ConnectionString = cs
        });
        return (IGraphRepository)repository;
    }

    public static void UpdateGraphForDeletion(string id, string dbName)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(dbName);

        IGraphRepository graphRepository = GetGraphRepository(dbName);
        if (graphRepository == null) return;

        graphRepository.DeleteGraphSet(id);
    }
}
