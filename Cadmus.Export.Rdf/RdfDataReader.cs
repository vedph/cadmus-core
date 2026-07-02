using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Data access layer for reading RDF data from the PostgreSQL database.
/// </summary>
public sealed class RdfDataReader
{
    private readonly string _connectionString;

    /// <summary>
    /// Creates a new instance of <see cref="RdfDataReader"/>.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <exception cref="ArgumentNullException">connectionString</exception>
    public RdfDataReader(string connectionString)
    {
        _connectionString = connectionString ??
            throw new ArgumentNullException(nameof(connectionString));
    }

    private static string? GetNullableString(IDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? GetNullableInt32(IDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private async Task<List<T>> ExecuteQueryAsync<T>(string query,
        Func<IDataReader, T> mapper)
    {
        List<T> results = [];

        using (NpgsqlConnection connection = new(_connectionString))
        {
            await connection.OpenAsync();
            using NpgsqlCommand command = new(query, connection);
            using NpgsqlDataReader reader = await command.ExecuteReaderAsync();
            while (reader.Read())
            {
                results.Add(mapper(reader));
            }
        }

        return results;
    }

    /// <summary>
    /// Gets the namespace mappings.
    /// </summary>
    /// <returns>List of mappings.</returns>
    public async Task<List<NamespaceMapping>> GetNamespaceMappingsAsync()
    {
        string query = "SELECT id, uri FROM namespace_lookup ORDER BY id";
        return await ExecuteQueryAsync(query, reader => new NamespaceMapping
        {
            Prefix = reader.GetString(reader.GetOrdinal("id")),
            Uri = reader.GetString(reader.GetOrdinal("uri"))
        });
    }

    /// <summary>
    /// Get the URI mappings.
    /// </summary>
    /// <returns>List of mappings.</returns>
    public async Task<List<UriMapping>> GetUriMappingsAsync()
    {
        string query = "SELECT id, uri FROM uri_lookup ORDER BY id";
        return await ExecuteQueryAsync(query, reader => new UriMapping
        {
            Id = reader.GetInt32(reader.GetOrdinal("id")),
            Uri = reader.GetString(reader.GetOrdinal("uri"))
        });
    }

    /// <summary>
    /// Gets the nodes, possibly filtered by tag and/or by whether they
    /// are referenced in triples.
    /// </summary>
    /// <param name="settings">The settings.</param>
    /// <returns>List of nodes.</returns>
    public async Task<List<RdfNode>> GetNodesAsync(RdfExportSettings settings)
    {
        StringBuilder queryBuilder = new("SELECT id, is_class, tag, label, " +
            "source_type, sid FROM node");

        if (settings.NodeTagFilter != null && settings.NodeTagFilter.Count > 0)
        {
            queryBuilder.Append(" WHERE tag = ANY(@tags)");
        }

        if (settings.ExportReferencedNodesOnly)
        {
            if (settings.NodeTagFilter != null && settings.NodeTagFilter.Count > 0)
            {
                queryBuilder.Append(" AND ");
            }
            else
            {
                queryBuilder.Append(" WHERE ");
            }
            queryBuilder.Append("id IN (SELECT DISTINCT s_id FROM triple " +
                "UNION SELECT DISTINCT o_id FROM triple WHERE o_id IS NOT NULL)");
        }

        queryBuilder.Append(" ORDER BY id");

        using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        using NpgsqlCommand command = new(queryBuilder.ToString(), connection);
        if (settings.NodeTagFilter != null && settings.NodeTagFilter.Count > 0)
        {
            command.Parameters.AddWithValue("@tags", settings.NodeTagFilter.ToArray());
        }

        List<RdfNode> results = [];
        using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
        {
            int idOrdinal = reader.GetOrdinal("id");
            int isClassOrdinal = reader.GetOrdinal("is_class");
            int tagOrdinal = reader.GetOrdinal("tag");
            int labelOrdinal = reader.GetOrdinal("label");
            int sourceTypeOrdinal = reader.GetOrdinal("source_type");
            int sidOrdinal = reader.GetOrdinal("sid");

            while (reader.Read())
            {
                results.Add(new RdfNode
                {
                    Id = reader.GetInt32(idOrdinal),
                    IsClass = reader.GetBoolean(isClassOrdinal),
                    Tag = GetNullableString(reader, tagOrdinal),
                    Label = reader.GetString(labelOrdinal),
                    SourceType = reader.GetInt32(sourceTypeOrdinal),
                    Sid = GetNullableString(reader, sidOrdinal)
                });
            }
        }
        return results;
    }

    /// <summary>
    /// Gets the properties.
    /// </summary>
    /// <returns>List of properties.</returns>
    public async Task<List<RdfProperty>> GetPropertiesAsync()
    {
        string query = "SELECT id, data_type, lit_editor, description " +
            "FROM property ORDER BY id";
        return await ExecuteQueryAsync(query, reader =>
        {
            int idOrdinal = reader.GetOrdinal("id");
            int dataTypeOrdinal = reader.GetOrdinal("data_type");
            int litEditorOrdinal = reader.GetOrdinal("lit_editor");
            int descriptionOrdinal = reader.GetOrdinal("description");

            return new RdfProperty
            {
                Id = reader.GetInt32(idOrdinal),
                DataType = GetNullableString(reader, dataTypeOrdinal),
                LitEditor = GetNullableString(reader, litEditorOrdinal),
                Description = GetNullableString(reader, descriptionOrdinal)
            };
        });
    }

    /// <summary>
    /// Gets the triples, possibly filtered by tag, with paging support.
    /// </summary>
    /// <param name="settings">Settings.</param>
    /// <param name="offset">Offset.</param>
    /// <param name="limit">Limit.</param>
    /// <returns>List of triples.</returns>
    public async Task<List<RdfTriple>> GetTriplesAsync(RdfExportSettings settings,
        int offset = 0, int? limit = null)
    {
        StringBuilder queryBuilder = new(
            "SELECT id, s_id, p_id, o_id, o_lit, o_lit_type, o_lit_lang, tag " +
            "FROM triple");

        if (settings.TripleTagFilter != null &&
            settings.TripleTagFilter.Count > 0)
        {
            queryBuilder.Append(" WHERE tag = ANY(@tags)");
        }

        queryBuilder.Append(" ORDER BY id");

        if (limit.HasValue) queryBuilder.Append($" LIMIT {limit.Value}");

        if (offset > 0) queryBuilder.Append($" OFFSET {offset}");

        using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        using NpgsqlCommand command = new(queryBuilder.ToString(), connection);
        if (settings.TripleTagFilter != null && settings.TripleTagFilter.Count > 0)
        {
            command.Parameters.AddWithValue("@tags", settings.TripleTagFilter.ToArray());
        }

        List<RdfTriple> results = [];
        using (NpgsqlDataReader reader = await command.ExecuteReaderAsync())
        {
            int idOrdinal = reader.GetOrdinal("id");
            int sIdOrdinal = reader.GetOrdinal("s_id");
            int pIdOrdinal = reader.GetOrdinal("p_id");
            int oIdOrdinal = reader.GetOrdinal("o_id");
            int oLitOrdinal = reader.GetOrdinal("o_lit");
            int oLitTypeOrdinal = reader.GetOrdinal("o_lit_type");
            int oLitLangOrdinal = reader.GetOrdinal("o_lit_lang");
            int tagOrdinal = reader.GetOrdinal("tag");

            while (reader.Read())
            {
                results.Add(new RdfTriple
                {
                    Id = reader.GetInt32(idOrdinal),
                    SubjectId = reader.GetInt32(sIdOrdinal),
                    PredicateId = reader.GetInt32(pIdOrdinal),
                    ObjectId = GetNullableInt32(reader, oIdOrdinal),
                    ObjectLiteral = GetNullableString(reader, oLitOrdinal),
                    ObjectLiteralType = GetNullableString(reader, oLitTypeOrdinal),
                    ObjectLiteralLanguage = GetNullableString(reader, oLitLangOrdinal),
                    Tag = GetNullableString(reader, tagOrdinal)
                });
            }
        }
        return results;
    }

    /// <summary>
    /// Gets the total count of triples, possibly filtered by tag.
    /// </summary>
    /// <param name="settings">Settings.</param>
    /// <returns>Count of triples.</returns>
    public async Task<int> GetTripleCountAsync(RdfExportSettings settings)
    {
        StringBuilder queryBuilder = new("SELECT COUNT(*) FROM triple");

        if (settings.TripleTagFilter != null && settings.TripleTagFilter.Count > 0)
        {
            queryBuilder.Append(" WHERE tag = ANY(@tags)");
        }

        using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync();
        using NpgsqlCommand command = new(queryBuilder.ToString(), connection);
        if (settings.TripleTagFilter != null && settings.TripleTagFilter.Count > 0)
        {
            command.Parameters.AddWithValue("@tags", settings.TripleTagFilter.ToArray());
        }

        object? result = await command.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
}
