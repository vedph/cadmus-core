using CsvHelper;
using Fusi.DbManager.PgSql;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Cadmus.Export.Rdf.Test;

/// <summary>
/// Helper class used to seed the PostgreSQL database for testing.
/// </summary>
internal static class TestHelper
{
    private const string DB_NAME = "cadmus-rdf-test";

    private const string CONNECTION_STRING_TEMPLATE =
        "Host=localhost;Username=postgres;Password=postgres;Database={0};" +
        "Include Error Detail=true";

    private static readonly NpgsqlConnection _connection = new(
        string.Format(CONNECTION_STRING_TEMPLATE, DB_NAME));

    public static string GetConnectionString() =>
        string.Format(CONNECTION_STRING_TEMPLATE, DB_NAME);

    private static string LoadSchema()
    {
        using Stream stream = typeof(TestHelper).Assembly
            .GetManifestResourceStream(
                "Cadmus.Export.Rdf.Test.Assets.Schema.sql")!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    public static void DropDatabase()
    {
        // drop the test database if it exists
        PgSqlDbManager manager = new(CONNECTION_STRING_TEMPLATE);
        if (manager.Exists(DB_NAME)) manager.RemoveDatabase(DB_NAME);
    }

    private static void EnsureDatabase()
    {
        // ensure the test database exists
        PgSqlDbManager manager = new(CONNECTION_STRING_TEMPLATE);
        string ddl = LoadSchema();
        if (!manager.Exists(DB_NAME)) manager.CreateDatabase(DB_NAME, ddl, null);

        if (_connection.State != ConnectionState.Open) _connection.Open();
    }

    private static Dictionary<string, int> GetUriMap()
    {
        Dictionary<string, int> map = [];
        using NpgsqlCommand cmd = new("SELECT id,uri FROM uri_lookup;",
            _connection);
        using NpgsqlDataReader reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            int id = reader.GetInt32(0);
            string uri = reader.GetString(1);
            map[uri] = id;
        }
        reader.Close();
        return map;
    }

    private static void SeedData(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);

        EnsureDatabase();

        // prepare commands
        // namespace_lookup: id,uri
        using NpgsqlCommand nsCmd = new(
            "INSERT INTO namespace_lookup (id, uri) " +
            "VALUES (@id, @uri);", _connection);
        nsCmd.Parameters.Add(new NpgsqlParameter("@id", DbType.String));
        nsCmd.Parameters.Add(new NpgsqlParameter("@uri", DbType.String));

        // uri_lookup: uri
        using NpgsqlCommand uriCmd = new(
            "INSERT INTO uri_lookup (uri) " +
            "VALUES (@uri) RETURNING id;",
            _connection);
        uriCmd.Parameters.Add(new NpgsqlParameter("@uri", DbType.String));

        // node: id,is_class,tag,label,source_type,sid
        using NpgsqlCommand nodeCmd = new(
            "INSERT INTO node (id, is_class, tag, \"label\", source_type, sid) " +
            "VALUES (@id, @is_class, @tag, @label, @source_type, @sid);",
            _connection);
        nodeCmd.Parameters.Add(new NpgsqlParameter("@id", DbType.Int32));
        nodeCmd.Parameters.Add(new NpgsqlParameter("@is_class", DbType.Boolean));
        nodeCmd.Parameters.Add(new NpgsqlParameter("@tag", DbType.String));
        nodeCmd.Parameters.Add(new NpgsqlParameter("@label", DbType.String));
        nodeCmd.Parameters.Add(new NpgsqlParameter("@source_type", DbType.Int32));
        nodeCmd.Parameters.Add(new NpgsqlParameter("@sid", DbType.String));

        // triple: id,s_id,p_id,o_id,o_lit,o_lit_type,o_lit_lang,
        // o_lit_ix,o_lit_n,sid,tag
        using NpgsqlCommand tripleCmd = new(
            @"INSERT INTO triple
            (s_id, p_id, o_id, o_lit, o_lit_type, o_lit_lang,
             o_lit_ix, o_lit_n, sid, tag)
            VALUES
            (@s_id, @p_id, @o_id, @o_lit, @o_lit_type, @o_lit_lang,
             @o_lit_ix, @o_lit_n, @sid, @tag);", _connection);
        tripleCmd.Parameters.Add(new NpgsqlParameter("@s_id", DbType.Int32));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@p_id", DbType.Int32));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@o_id", DbType.Int32));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@o_lit", DbType.String));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@o_lit_type", DbType.String));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@o_lit_lang", DbType.String));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@o_lit_ix", DbType.String));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@o_lit_n", DbType.Double));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@sid", DbType.String));
        tripleCmd.Parameters.Add(new NpgsqlParameter("@tag", DbType.String));

        CsvReader csv = new(reader, CultureInfo.InvariantCulture);
        string? currentSection = null;
        Regex nodeUriRegex = new(@"^[^:]+:", RegexOptions.Compiled);

        // triple ID is used just as an ordinal for its mock SID value,
        // but this is an autoincrement field in the database
        int nTriple = 1;
        Dictionary<string, int> uriMap = GetUriMap();

        while (csv.Read())
        {
            string? f0 = csv.GetField(0);
            if (f0 == null || string.IsNullOrWhiteSpace(f0)) continue;
            if (f0.StartsWith('#'))
            {
                currentSection = f0[1..].ToLowerInvariant();
                continue;
            }

            switch (currentSection)
            {
                case "namespaces":
                    {
                        // id,uri
                        string id = f0;
                        string uri = csv.GetField(1) ?? "";
                        nsCmd.Parameters["@id"].Value = id;
                        nsCmd.Parameters["@uri"].Value = uri;
                        nsCmd.ExecuteNonQuery();
                    }
                    break;

                case "nodes":
                    {
                        string label = f0;
                        string uri = csv.GetField(1) ?? "";
                        string sid = csv.GetField(2) ?? $"sid/{uri}";

                        // add uri_lookup with the node's URI so we can get ids ID
                        uriCmd.Parameters["@uri"].Value = uri;
                        int id = (int)uriCmd.ExecuteScalar()!;

                        // label,uri,sid (id is set by us)
                        nodeCmd.Parameters["@id"].Value = id;
                        nodeCmd.Parameters["@is_class"].Value = false;
                        nodeCmd.Parameters["@tag"].Value = DBNull.Value;
                        nodeCmd.Parameters["@label"].Value = label;
                        nodeCmd.Parameters["@source_type"].Value = 0;
                        nodeCmd.Parameters["@sid"].Value = sid
                            ?? $"sid/{uri}";
                        nodeCmd.ExecuteNonQuery();
                        // build ID map
                        uriMap[uri] = id;
                    }
                    break;

                case "triples":
                    {
                        // s_uri,p_uri,o_uri or literal,sid
                        string s_uri = f0;
                        int s_id = uriMap[s_uri];
                        string p_uri = csv.GetField(1) ?? "";
                        int p_id = uriMap[p_uri];
                        string o = csv.GetField(2) ?? "";
                        string sid = csv.GetField(3) ?? $"sid/triple/{nTriple}";
                        nTriple++;

                        bool isOid = nodeUriRegex.IsMatch(o);
                        tripleCmd.Parameters["@s_id"].Value = s_id;
                        tripleCmd.Parameters["@p_id"].Value = p_id;
                        tripleCmd.Parameters["@sid"].Value = sid;
                        tripleCmd.Parameters["@tag"].Value = DBNull.Value;

                        if (isOid)
                        {
                            tripleCmd.Parameters["@o_id"].Value = uriMap[o];
                            tripleCmd.Parameters["@o_lit"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit_type"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit_lang"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit_ix"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit_n"].Value = DBNull.Value;
                        }
                        else
                        {
                            tripleCmd.Parameters["@o_id"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit"].Value = o;
                            tripleCmd.Parameters["@o_lit_type"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit_lang"].Value =
                                string.IsNullOrEmpty(o)
                                ? DBNull.Value
                                : "en";
                            tripleCmd.Parameters["@o_lit_ix"].Value = DBNull.Value;
                            tripleCmd.Parameters["@o_lit_n"].Value = DBNull.Value;
                        }
                        tripleCmd.ExecuteNonQuery();
                    }
                    break;
            }
        }

        _connection.Close();
    }

    public static void CreateDatabase(string resourceName = "Data.csv")
    {
        using StreamReader reader = new(Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("Cadmus.Export.Rdf.Test.Assets." +
            resourceName)!, Encoding.UTF8);
        SeedData(reader);
    }
}
