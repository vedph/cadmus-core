using Cadmus.Index.Search;
using Fusi.Tools.Data;
using NpgsqlTypes;
using System;
using System.Linq;
using Xunit;

namespace Cadmus.Index.PgSql.Test;

public sealed class PgSqlQueryBuilderTest
{
    private readonly PgSqlQueryBuilder _builder = new();
    private static readonly PagingOptions _options = new()
    {
        PageNumber = 3,
        PageSize = 10
    };

    [Theory]
    [InlineData("abc", "abc")]
    [InlineData(@"a%b_c\d", @"a\%b\_c\\d")]
    public void EscapeLike_Ok(string text, string expected)
    {
        Assert.Equal(expected, PgSqlQueryBuilder.EscapeLike(text));
    }

    [Theory]
    [InlineData("a*b?c", "a%b_c")]
    [InlineData(@"5%_\*", @"5\%\_\\%")]
    public void WildcardsToLike_Ok(string text, string expected)
    {
        Assert.Equal(expected, PgSqlQueryBuilder.WildcardsToLike(text));
    }

    [Fact]
    public void Build_NullArgs_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => _builder.BuildItemQuery((IndexQueryNode)null!, _options));
        Assert.Throws<ArgumentNullException>(
            () => _builder.BuildItemQuery("[t=x]", null!));
        Assert.Throws<ArgumentNullException>(
            () => _builder.BuildPinQuery((IndexQueryNode)null!, _options));
        Assert.Throws<ArgumentNullException>(
            () => _builder.BuildPinQuery("[t=x]", null!));
        Assert.Throws<ArgumentNullException>(
            () => PgSqlQueryBuilder.EscapeLike(null!));
        Assert.Throws<ArgumentNullException>(
            () => PgSqlQueryBuilder.WildcardsToLike(null!));
    }

    [Fact]
    public void BuildItemQuery_ItemField_Ok()
    {
        PgSqlQuery query = _builder.BuildItemQuery("[title=a'b]", _options);

        Assert.Equal("SELECT COUNT(*) FROM item i WHERE (i.title = $1)",
            query.CountSql);
        Assert.StartsWith("SELECT i.id, i.title,", query.PageSql);
        Assert.EndsWith("FROM item i WHERE (i.title = $1) " +
            "ORDER BY i.sort_key, i.id LIMIT $2 OFFSET $3", query.PageSql);
        // the value is a parameter, never part of the SQL
        Assert.DoesNotContain("a'b", query.PageSql);
        Assert.Equal("a'b", Assert.Single(query.CountParameters).Value);
        Assert.Equal(3, query.PageParameters.Count);
        Assert.Equal(10, query.PageParameters[1].Value);
        Assert.Equal(20L, query.PageParameters[2].Value);
        Assert.Null(query.SimilarityThreshold);
    }

    [Fact]
    public void BuildItemQuery_NoPaging_NoLimit()
    {
        PgSqlQuery query = _builder.BuildItemQuery("[title=a]",
            new PagingOptions { PageSize = 0 });
        Assert.DoesNotContain("LIMIT", query.PageSql);
        Assert.Single(query.PageParameters);
    }

    [Fact]
    public void BuildItemQuery_PageNumberZero_FirstPage()
    {
        PgSqlQuery query = _builder.BuildItemQuery("[title=a]",
            new PagingOptions { PageNumber = 0, PageSize = 5 });
        Assert.Equal(0L, query.PageParameters[2].Value);
    }

    [Fact]
    public void BuildItemQuery_PinClauses_ExistsEach()
    {
        PgSqlQuery query = _builder.BuildItemQuery(
            "[tag=odd] AND [year==1200]", _options);

        Assert.Equal("SELECT COUNT(*) FROM item i WHERE (" +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND " +
            "p.name = $2 AND p.value = $1) AND " +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND " +
            "p.name = $4 AND (CASE WHEN p.value ~ " +
            "'^[[:space:]]*[-+]?([0-9]+([.][0-9]*)?|[.][0-9]+)[[:space:]]*$' " +
            "THEN p.value::numeric END) = $3))", query.CountSql);
        Assert.Equal(["odd", "tag", 1200m, "year"],
            query.CountParameters.Select(p => p.Value));
        Assert.Equal(NpgsqlDbType.Numeric, query.CountParameters[2].Type);
    }

    [Fact]
    public void BuildItemQuery_Not_Ok()
    {
        PgSqlQuery query = _builder.BuildItemQuery(
            "NOT [type=x] OR NOT [title=y]", _options);
        Assert.Equal("SELECT COUNT(*) FROM item i WHERE (" +
            "NOT EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND " +
            "p.part_type_id = $1) OR NOT (i.title = $2))", query.CountSql);
    }

    [Theory]
    [InlineData("[title*=a%]", "i.title LIKE $1", "%a\\%%")]
    [InlineData("[title^=a_]", "i.title LIKE $1", "a\\_%")]
    [InlineData("[title$=a]", "i.title LIKE $1", "%a")]
    [InlineData("[title?=a*b?]", "i.title LIKE $1", "a%b_")]
    [InlineData("[title~=^a]", "i.title ~ $1", "^a")]
    [InlineData("[title<>a]", "i.title <> $1", "a")]
    [InlineData("[group<>a]", "i.group_id IS DISTINCT FROM $1", "a")]
    [InlineData("[itemId=a]", "i.id = $1", "a")]
    [InlineData("[sortKey=a]", "i.sort_key = $1", "a")]
    [InlineData("[dsc=a]", "i.description = $1", "a")]
    [InlineData("[facet=a]", "i.facet_id = $1", "a")]
    [InlineData("[creatorId=a]", "i.creator_id = $1", "a")]
    [InlineData("[userId=a]", "i.user_id = $1", "a")]
    public void BuildItemQuery_TextOperators_Ok(string text, string sql,
        string value)
    {
        PgSqlQuery query = _builder.BuildItemQuery(text, _options);
        Assert.Equal($"SELECT COUNT(*) FROM item i WHERE ({sql})",
            query.CountSql);
        Assert.Equal(value, query.CountParameters[0].Value);
    }

    [Theory]
    [InlineData("[group=]", "(i.group_id IS NULL OR i.group_id = '')")]
    [InlineData("[group<>]", "(i.group_id IS NOT NULL AND i.group_id <> '')")]
    public void BuildItemQuery_EmptyNullable_Ok(string text, string sql)
    {
        PgSqlQuery query = _builder.BuildItemQuery(text, _options);
        Assert.Equal($"SELECT COUNT(*) FROM item i WHERE ({sql})",
            query.CountSql);
        Assert.Empty(query.CountParameters);
    }

    [Theory]
    [InlineData("[flags:3]", "(i.flags & $1) <> 0")]
    [InlineData("[flags&:3]", "(i.flags & $1) = $1")]
    [InlineData("[flags!:3]", "(i.flags & $1) = 0")]
    [InlineData("[flags=3]", "i.flags = $1")]
    [InlineData("[flags<>3]", "i.flags <> $1")]
    public void BuildItemQuery_Flags_Ok(string text, string sql)
    {
        PgSqlQuery query = _builder.BuildItemQuery(text, _options);
        Assert.Equal($"SELECT COUNT(*) FROM item i WHERE ({sql})",
            query.CountSql);
        Assert.Equal(3, query.CountParameters[0].Value);
        Assert.Equal(NpgsqlDbType.Integer, query.CountParameters[0].Type);
    }

    [Theory]
    [InlineData("[flags==3]", "=")]
    [InlineData("[flags!=3]", "<>")]
    [InlineData("[flags<3]", "<")]
    [InlineData("[flags<=3]", "<=")]
    [InlineData("[flags>3]", ">")]
    [InlineData("[flags>=3]", ">=")]
    public void BuildItemQuery_FlagsNumeric_Ok(string text, string op)
    {
        PgSqlQuery query = _builder.BuildItemQuery(text, _options);
        Assert.Equal($"SELECT COUNT(*) FROM item i WHERE (i.flags {op} $1)",
            query.CountSql);
        Assert.Equal(3m, query.CountParameters[0].Value);
    }

    [Fact]
    public void BuildItemQuery_Time_Ok()
    {
        PgSqlQuery query = _builder.BuildItemQuery(
            "[timeModified>2024-01-01] AND [time<2025-01-01]", _options);
        Assert.Equal("SELECT COUNT(*) FROM item i WHERE (" +
            "(i.time_modified > $1) AND EXISTS (SELECT 1 FROM pin p WHERE " +
            "p.item_id = i.id AND p.time_indexed < $2))", query.CountSql);
        Assert.Equal(NpgsqlDbType.TimestampTz, query.CountParameters[0].Type);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            query.CountParameters[1].Value);
    }

    [Fact]
    public void BuildItemQuery_Similar_Ok()
    {
        PgSqlQuery query = _builder.BuildItemQuery(
            "[title%=x:0.5] OR [tag%=y:0.4]", _options);
        Assert.Equal("SELECT COUNT(*) FROM item i WHERE (" +
            "(i.title % $1 AND similarity(i.title, $1) >= $2) OR " +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND " +
            "p.name = $5 AND p.value % $3 AND similarity(p.value, $3) >= $4))",
            query.CountSql);
        Assert.Equal(0.4, query.SimilarityThreshold);
        Assert.Contains("pg_trgm.similarity_threshold = 0.4", query.ToString());
    }

    [Fact]
    public void BuildItemQuery_PinFields_Ok()
    {
        PgSqlQuery query = _builder.BuildItemQuery(
            "[partId=1] [role=2] [name=3] [value=4]", _options);
        Assert.Equal("SELECT COUNT(*) FROM item i WHERE (" +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND p.part_id = $1) AND " +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND p.part_role_id = $2) AND " +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND p.name = $3) AND " +
            "EXISTS (SELECT 1 FROM pin p WHERE p.item_id = i.id AND p.value = $4))",
            query.CountSql);
    }

    [Fact]
    public void BuildPinQuery_PinOnly_NoJoin()
    {
        PgSqlQuery query = _builder.BuildPinQuery(
            "[partTypeId=it.vedph.x] AND [name=year] AND [value^=1] " +
            "AND [itemId=x]", _options);
        Assert.Equal("SELECT COUNT(*) FROM pin p WHERE (" +
            "(p.part_type_id = $1) AND (p.name = $2) AND (p.value LIKE $3) " +
            "AND (p.item_id = $4))", query.CountSql);
        Assert.Equal("SELECT p.item_id, p.part_id, p.part_type_id, " +
            "p.part_role_id, p.name, p.value FROM pin p WHERE (" +
            "(p.part_type_id = $1) AND (p.name = $2) AND (p.value LIKE $3) " +
            "AND (p.item_id = $4)) ORDER BY p.name, p.value, p.id " +
            "LIMIT $5 OFFSET $6", query.PageSql);
    }

    [Fact]
    public void BuildPinQuery_ItemField_Join()
    {
        PgSqlQuery query = _builder.BuildPinQuery("[tag=x] AND [t=y]", _options);
        Assert.Equal("SELECT COUNT(*) FROM pin p JOIN item i ON " +
            "i.id = p.item_id WHERE ((p.name = $2 AND p.value = $1) AND " +
            "(i.title = $3))", query.CountSql);
    }

    [Fact]
    public void ToString_Ok()
    {
        string text = _builder.BuildItemQuery("[t=x]", _options).ToString();
        Assert.Contains("SELECT COUNT(*)", text);
        Assert.Contains("-- $1 = x (Text)", text);
        Assert.Contains("-- $3 = 20 (Bigint)", text);
    }

    [Fact]
    public void PgSqlQuery_NullArgs_Throw()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new PgSqlQuery(null!, "", [], [], null));
        Assert.Throws<ArgumentNullException>(() =>
            new PgSqlQuery("", null!, [], [], null));
        Assert.Throws<ArgumentNullException>(() =>
            new PgSqlQuery("", "", null!, [], null));
        Assert.Throws<ArgumentNullException>(() =>
            new PgSqlQuery("", "", [], null!, null));
    }

    [Fact]
    public void BuildItemQuery_Parser_FlagLabels()
    {
        IndexQueryParser parser = new(
            [new Core.Config.FlagDefinition { Id = 8, Label = "x" }]);
        PgSqlQuery query = _builder.BuildItemQuery("[flags:x]", _options, parser);
        Assert.Equal(8, query.CountParameters[0].Value);
    }

    private sealed class UnknownNode : IndexQueryNode
    {
    }

    [Fact]
    public void Build_UnknownNode_Throws()
    {
        Assert.Throws<ArgumentException>(
            () => _builder.BuildItemQuery(new UnknownNode(), _options));
    }

    [Fact]
    public void Build_UnsupportedField_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildPinQuery(new IndexQueryClause
            {
                Field = (IndexQueryField)999,
                Value = "x"
            }, _options));
    }

    [Fact]
    public void Build_UnsupportedOperator_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _builder.BuildItemQuery(new IndexQueryClause
            {
                Field = IndexQueryField.Title,
                Operator = (IndexQueryOperator)999,
                Value = "x"
            }, _options));
    }
}
