using Cadmus.Core.Config;
using Cadmus.Index.Search;
using System;
using Xunit;

namespace Cadmus.Index.Test;

public sealed class IndexQueryParserTest
{
    private static IndexQueryClause ParseClause(string query,
        IndexQueryParser? parser = null)
    {
        IndexQueryNode node = (parser ?? new IndexQueryParser()).Parse(query);
        return Assert.IsType<IndexQueryClause>(node);
    }

    private static CadmusQueryException AssertInvalid(string query,
        IndexQueryParser? parser = null) =>
        Assert.Throws<CadmusQueryException>(
            () => (parser ?? new IndexQueryParser()).Parse(query));

    #region Basics
    [Fact]
    public void Parse_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new IndexQueryParser().Parse(null!));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_Empty_Throws(string query)
    {
        CadmusQueryException ex = AssertInvalid(query);
        Assert.Equal(0, ex.Position);
    }

    [Fact]
    public void Parse_PlainText_TitleContains()
    {
        IndexQueryClause clause = ParseClause("  Item #1 ");
        Assert.Equal(IndexQueryField.Title, clause.Field);
        Assert.Equal(IndexQueryOperator.Contains, clause.Operator);
        Assert.Equal("Item #1", clause.Value);
    }

    [Fact]
    public void Parse_PlainTextWithOperators_TitleContains()
    {
        // without [ there are no clauses, so it is all plain text
        IndexQueryClause clause = ParseClause("a OR b");
        Assert.Equal("a OR b", clause.Value);
    }

    [Theory]
    [InlineData("[title=x]", IndexQueryField.Title)]
    [InlineData("[T=x]", IndexQueryField.Title)]
    [InlineData("[description=x]", IndexQueryField.Description)]
    [InlineData("[dsc=x]", IndexQueryField.Description)]
    [InlineData("[facet=x]", IndexQueryField.FacetId)]
    [InlineData("[facetId=x]", IndexQueryField.FacetId)]
    [InlineData("[group=x]", IndexQueryField.GroupId)]
    [InlineData("[groupId=x]", IndexQueryField.GroupId)]
    [InlineData("[sortKey=x]", IndexQueryField.SortKey)]
    [InlineData("[itemId=x]", IndexQueryField.ItemId)]
    [InlineData("[creatorId=x]", IndexQueryField.CreatorId)]
    [InlineData("[userId=x]", IndexQueryField.UserId)]
    [InlineData("[partId=x]", IndexQueryField.PartId)]
    [InlineData("[partTypeId=x]", IndexQueryField.PartTypeId)]
    [InlineData("[partType=x]", IndexQueryField.PartTypeId)]
    [InlineData("[type=x]", IndexQueryField.PartTypeId)]
    [InlineData("[roleId=x]", IndexQueryField.RoleId)]
    [InlineData("[role=x]", IndexQueryField.RoleId)]
    [InlineData("[name=x]", IndexQueryField.Name)]
    [InlineData("[n=x]", IndexQueryField.Name)]
    [InlineData("[value=x]", IndexQueryField.Value)]
    [InlineData("[v=x]", IndexQueryField.Value)]
    public void Parse_FieldAlias_Ok(string query, IndexQueryField field)
    {
        IndexQueryClause clause = ParseClause(query);
        Assert.Equal(field, clause.Field);
        Assert.Null(clause.PinName);
        Assert.Equal("x", clause.Value);
    }

    [Fact]
    public void Parse_PinName_Ok()
    {
        IndexQueryClause clause = ParseClause("[ date-value =x]");
        Assert.Equal(IndexQueryField.Pin, clause.Field);
        Assert.Equal("date-value", clause.PinName);
        Assert.Equal(IndexQueryOperator.Equal, clause.Operator);
    }

    [Fact]
    public void Parse_EscapedPinName_Ok()
    {
        IndexQueryClause clause = ParseClause("[@title=x]");
        Assert.Equal(IndexQueryField.Pin, clause.Field);
        Assert.Equal("title", clause.PinName);
    }

    [Fact]
    public void Parse_PinNameWithAt_Ok()
    {
        IndexQueryClause clause = ParseClause("[tag@x=y]");
        Assert.Equal(IndexQueryField.Pin, clause.Field);
        Assert.Equal("tag@x", clause.PinName);
    }

    [Theory]
    [InlineData("[=x]")]
    [InlineData("[@=x]")]
    [InlineData("[title]")]
    [InlineData("[title&x]")]
    [InlineData("[title=x")]
    [InlineData("[title=x] AND")]
    [InlineData("[title=x] OR")]
    [InlineData("[title=x] AND NOT")]
    [InlineData("([title=x]")]
    [InlineData("[title=x])")]
    [InlineData("[t=x] ()")]
    [InlineData("[title=x] XOR [title=y]")]
    [InlineData("[title=x] 1=1")]
    [InlineData("[title=x]; DROP TABLE item")]
    [InlineData("[title=x] OR 1=1 --")]
    public void Parse_Invalid_Throws(string query)
    {
        CadmusQueryException ex = AssertInvalid(query);
        Assert.True(ex.Position >= 0);
    }

    [Fact]
    public void Parse_TooManyClauses_Throws()
    {
        string query = string.Join(" OR ",
            System.Linq.Enumerable.Repeat("[t=x]", IndexQueryParser.MAX_CLAUSES + 1));
        AssertInvalid(query);
    }

    [Fact]
    public void Parse_MaxClauses_Ok()
    {
        string query = string.Join(" OR ",
            System.Linq.Enumerable.Repeat("[t=x]", IndexQueryParser.MAX_CLAUSES));
        IndexQueryLogicalNode node = Assert.IsType<IndexQueryLogicalNode>(
            new IndexQueryParser().Parse(query));
        Assert.Equal(IndexQueryParser.MAX_CLAUSES, node.Children.Count);
    }

    [Fact]
    public void Parse_TooDeep_Throws()
    {
        string query = new string('(', IndexQueryParser.MAX_DEPTH + 1) + "[t=x]" +
            new string(')', IndexQueryParser.MAX_DEPTH + 1);
        AssertInvalid(query);
    }

    [Fact]
    public void Parse_MaxDepth_Ok()
    {
        string query = new string('(', IndexQueryParser.MAX_DEPTH) + "[t=x]" +
            new string(')', IndexQueryParser.MAX_DEPTH);
        Assert.IsType<IndexQueryClause>(new IndexQueryParser().Parse(query));
    }
    #endregion

    #region Logic
    [Fact]
    public void Parse_And_Ok()
    {
        IndexQueryLogicalNode node = Assert.IsType<IndexQueryLogicalNode>(
            new IndexQueryParser().Parse("[a=1] and [b=2] AND [c=3]"));
        Assert.Equal(IndexQueryLogicalOperator.And, node.Operator);
        Assert.Equal(3, node.Children.Count);
        Assert.Equal("(a AND b AND c)", Describe(node));
    }

    [Fact]
    public void Parse_ImplicitAnd_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse("[a=1] [b=2]");
        Assert.Equal("(a AND b)", Describe(node));
    }

    [Fact]
    public void Parse_OrPrecedence_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse(
            "[a=1] AND [b=2] OR [c=3] AND [d=4]");
        Assert.Equal("((a AND b) OR (c AND d))", Describe(node));
    }

    [Fact]
    public void Parse_Brackets_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse(
            "[a=1] AND ([b=2] OR [c=3])");
        Assert.Equal("(a AND (b OR c))", Describe(node));
    }

    [Fact]
    public void Parse_Not_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse(
            "[a=1] AND NOT [b=2] OR not ([c=3] [d=4])");
        Assert.Equal("((a AND NOT b) OR NOT (c AND d))", Describe(node));
    }

    [Fact]
    public void Parse_DoubleNot_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse("NOT NOT [a=1]");
        Assert.Equal("NOT NOT a", Describe(node));
    }

    [Fact]
    public void Parse_FrontendExample_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse(
            "[title*=test] AND ([facet=default] OR [facet=tiled])");
        Assert.Equal("(Title AND (FacetId OR FacetId))", Describe(node));
    }

    private static string Describe(IndexQueryNode node) => node switch
    {
        IndexQueryClause c => c.Field == IndexQueryField.Pin
            ? c.PinName! : c.Field.ToString(),
        IndexQueryNotNode n => "NOT " + Describe(n.Operand),
        IndexQueryLogicalNode l => "(" + string.Join(
            l.Operator == IndexQueryLogicalOperator.And ? " AND " : " OR ",
            System.Linq.Enumerable.Select(l.Children, Describe)) + ")",
        _ => "?"
    };

    [Fact]
    public void Nodes_ToString_Ok()
    {
        IndexQueryNode node = new IndexQueryParser().Parse(
            "[a=1] AND NOT [title=2]");
        Assert.Equal("([@a Equal 1] AND NOT [Title Equal 2])", node.ToString());
    }
    #endregion

    #region Operators
    [Theory]
    [InlineData("=", IndexQueryOperator.Equal)]
    [InlineData("<>", IndexQueryOperator.NotEqual)]
    [InlineData("*=", IndexQueryOperator.Contains)]
    [InlineData("^=", IndexQueryOperator.StartsWith)]
    [InlineData("$=", IndexQueryOperator.EndsWith)]
    [InlineData("~=", IndexQueryOperator.Regex)]
    public void Parse_TextOperators_Ok(string op, IndexQueryOperator expected)
    {
        IndexQueryClause clause = ParseClause($"[x{op}abc]");
        Assert.Equal(expected, clause.Operator);
        Assert.Equal("abc", clause.Value);
        Assert.Null(clause.Number);
    }

    [Fact]
    public void Parse_ValueWithOperatorChars_Ok()
    {
        IndexQueryClause clause = ParseClause("[x=a=b<c>:d]");
        Assert.Equal(IndexQueryOperator.Equal, clause.Operator);
        Assert.Equal("a=b<c>:d", clause.Value);
    }

    [Fact]
    public void Parse_EmptyValue_Ok()
    {
        IndexQueryClause clause = ParseClause("[group=]");
        Assert.Equal("", clause.Value);
    }

    [Fact]
    public void Parse_WildcardsWith_Wildcards()
    {
        IndexQueryClause clause = ParseClause("[x?=a?b*]");
        Assert.Equal(IndexQueryOperator.Wildcards, clause.Operator);
        Assert.Equal("a?b*", clause.Value);
    }

    [Fact]
    public void Parse_WildcardsWithout_Equal()
    {
        IndexQueryClause clause = ParseClause("[x?=ab]");
        Assert.Equal(IndexQueryOperator.Equal, clause.Operator);
    }

    [Fact]
    public void Parse_EmptyRegex_Throws()
    {
        AssertInvalid("[x~=]");
    }

    [Theory]
    [InlineData("==", IndexQueryOperator.NumEqual)]
    [InlineData("!=", IndexQueryOperator.NumNotEqual)]
    [InlineData("<", IndexQueryOperator.NumLess)]
    [InlineData("<=", IndexQueryOperator.NumLessOrEqual)]
    [InlineData(">", IndexQueryOperator.NumGreater)]
    [InlineData(">=", IndexQueryOperator.NumGreaterOrEqual)]
    public void Parse_NumericOperators_Ok(string op, IndexQueryOperator expected)
    {
        IndexQueryClause clause = ParseClause($"[year{op}-12.5]");
        Assert.Equal(expected, clause.Operator);
        Assert.Equal(-12.5m, clause.Number);
    }

    [Fact]
    public void Parse_NumericInvalid_Throws()
    {
        AssertInvalid("[year==abc]");
    }

    [Fact]
    public void Parse_SimilarWithoutThreshold_Default()
    {
        IndexQueryClause clause = ParseClause("[title%=itm]");
        Assert.Equal(IndexQueryOperator.Similar, clause.Operator);
        Assert.Equal("itm", clause.Value);
        Assert.Equal(IndexQueryParser.DEFAULT_SIMILARITY_THRESHOLD,
            clause.Threshold);
    }

    [Fact]
    public void Parse_SimilarWithThreshold_Ok()
    {
        IndexQueryClause clause = ParseClause("[title%=itm #1:0.35]");
        Assert.Equal("itm #1", clause.Value);
        Assert.Equal(0.35, clause.Threshold);
    }

    [Theory]
    [InlineData("[title%=x:0]")]
    [InlineData("[title%=x:1.5]")]
    [InlineData("[title%=:0.5]")]
    public void Parse_SimilarInvalid_Throws(string query)
    {
        AssertInvalid(query);
    }
    #endregion

    #region Time
    [Theory]
    [InlineData("=", IndexQueryOperator.NumEqual)]
    [InlineData("<>", IndexQueryOperator.NumNotEqual)]
    [InlineData("==", IndexQueryOperator.NumEqual)]
    [InlineData(">=", IndexQueryOperator.NumGreaterOrEqual)]
    public void Parse_TimeField_Ok(string op, IndexQueryOperator expected)
    {
        IndexQueryClause clause = ParseClause($"[timeCreated{op}2024-05-01T10:00:00Z]");
        Assert.Equal(IndexQueryField.TimeCreated, clause.Field);
        Assert.Equal(expected, clause.Operator);
        Assert.Equal(new DateTime(2024, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            clause.Date);
        Assert.Equal(DateTimeKind.Utc, clause.Date!.Value.Kind);
    }

    [Theory]
    [InlineData("timeModified", IndexQueryField.TimeModified)]
    [InlineData("time", IndexQueryField.TimeIndexed)]
    [InlineData("timeIndexed", IndexQueryField.TimeIndexed)]
    public void Parse_TimeAliases_Ok(string name, IndexQueryField field)
    {
        IndexQueryClause clause = ParseClause($"[{name}<2024-05-01]");
        Assert.Equal(field, clause.Field);
        Assert.Equal(new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            clause.Date);
    }

    [Theory]
    [InlineData("[timeCreated*=2024]")]
    [InlineData("[timeCreated=yesterday]")]
    public void Parse_TimeInvalid_Throws(string query)
    {
        AssertInvalid(query);
    }
    #endregion

    #region Flags
    private static IndexQueryParser GetFlagsParser() => new(
    [
        new FlagDefinition { Id = 1, Label = "to revise" },
        new FlagDefinition { Id = 4, Label = "Bad" },
        new FlagDefinition { Id = 8, Label = " " },
    ]);

    [Theory]
    [InlineData(":", IndexQueryOperator.HasAnyFlags)]
    [InlineData("&:", IndexQueryOperator.HasAllFlags)]
    [InlineData("!:", IndexQueryOperator.HasNoFlags)]
    [InlineData("=", IndexQueryOperator.Equal)]
    [InlineData("<>", IndexQueryOperator.NotEqual)]
    public void Parse_FlagsOperators_Ok(string op, IndexQueryOperator expected)
    {
        IndexQueryClause clause = ParseClause($"[flags{op}1,10]");
        Assert.Equal(expected, clause.Operator);
        // hex values
        Assert.Equal(0x11, clause.Flags);
    }

    [Fact]
    public void Parse_FlagsLabels_Ok()
    {
        IndexQueryClause clause = ParseClause("[flags:TO REVISE, 2]",
            GetFlagsParser());
        Assert.Equal(3, clause.Flags);
    }

    [Fact]
    public void Parse_FlagsLabelLikeHex_LabelWins()
    {
        // "bad" is also a valid hex number (0xBAD)
        IndexQueryClause clause = ParseClause("[flags:bad]", GetFlagsParser());
        Assert.Equal(4, clause.Flags);
    }

    [Fact]
    public void Parse_FlagsMaxHex_Ok()
    {
        IndexQueryClause clause = ParseClause("[flags:FFFFFFFF]");
        Assert.Equal(-1, clause.Flags);
    }

    [Theory]
    [InlineData("[flags:unknown]")]
    [InlineData("[flags:]")]
    [InlineData("[flags:,]")]
    [InlineData("[flags*=1]")]
    [InlineData("[flags==x]")]
    [InlineData("[title:1]")]
    [InlineData("[tag&:1]")]
    [InlineData("[tag!:1]")]
    public void Parse_FlagsInvalid_Throws(string query)
    {
        AssertInvalid(query);
    }

    [Fact]
    public void Parse_FlagsNumeric_Ok()
    {
        IndexQueryClause clause = ParseClause("[flags>=8]");
        Assert.Equal(IndexQueryOperator.NumGreaterOrEqual, clause.Operator);
        Assert.Equal(8m, clause.Number);
        Assert.Null(clause.Flags);
    }
    #endregion

    #region Escapes
    [Theory]
    [InlineData("abc", "abc")]
    [InlineData(@"a\[b\]", "a[b]")]
    [InlineData(@"a\\b", @"a\b")]
    [InlineData(@"\005Bx\005D", "[x]")]
    [InlineData(@"\005bx\005d", "[x]")]
    [InlineData(@"\00zz", @"\00zz")]
    [InlineData(@"\d+", @"\d+")]
    [InlineData(@"\00e", @"\00e")]
    [InlineData(@"x\", @"x\")]
    public void Unescape_Ok(string value, string expected)
    {
        Assert.Equal(expected, IndexQueryParser.Unescape(value));
    }

    [Fact]
    public void Parse_EscapedBrackets_Ok()
    {
        IndexQueryClause clause = ParseClause(@"[title~=\[01\]]");
        Assert.Equal(IndexQueryOperator.Regex, clause.Operator);
        Assert.Equal("[01]", clause.Value);
    }

    [Fact]
    public void Parse_UnescapedOpenBracketInValue_Ok()
    {
        IndexQueryClause clause = ParseClause("[title*=a[b]");
        Assert.Equal("a[b", clause.Value);
    }

    [Fact]
    public void Parse_HexEscapes_Ok()
    {
        IndexQueryClause clause = ParseClause(@"[title=\005Bx\005D]");
        Assert.Equal("[x]", clause.Value);
    }

    [Fact]
    public void Parse_ClausePosition_Ok()
    {
        IndexQueryLogicalNode node = Assert.IsType<IndexQueryLogicalNode>(
            new IndexQueryParser().Parse("[a=1] AND [b=2]"));
        Assert.Equal(0, ((IndexQueryClause)node.Children[0]).Position);
        Assert.Equal(10, ((IndexQueryClause)node.Children[1]).Position);
    }
    #endregion

    #region Field extensions
    [Theory]
    [InlineData(IndexQueryField.Pin, true)]
    [InlineData(IndexQueryField.PartId, true)]
    [InlineData(IndexQueryField.PartTypeId, true)]
    [InlineData(IndexQueryField.RoleId, true)]
    [InlineData(IndexQueryField.Name, true)]
    [InlineData(IndexQueryField.Value, true)]
    [InlineData(IndexQueryField.TimeIndexed, true)]
    [InlineData(IndexQueryField.ItemId, false)]
    [InlineData(IndexQueryField.Title, false)]
    [InlineData(IndexQueryField.Flags, false)]
    public void IsPinField_Ok(IndexQueryField field, bool expected)
    {
        Assert.Equal(expected, field.IsPinField());
    }

    [Theory]
    [InlineData(IndexQueryField.GroupId, true)]
    [InlineData(IndexQueryField.CreatorId, true)]
    [InlineData(IndexQueryField.UserId, true)]
    [InlineData(IndexQueryField.RoleId, true)]
    [InlineData(IndexQueryField.Title, false)]
    [InlineData(IndexQueryField.Pin, false)]
    public void IsNullable_Ok(IndexQueryField field, bool expected)
    {
        Assert.Equal(expected, field.IsNullable());
    }

    [Fact]
    public void Nodes_NullArguments_Throw()
    {
        Assert.Throws<ArgumentNullException>(() => new IndexQueryNotNode(null!));
        Assert.Throws<ArgumentNullException>(() => new IndexQueryLogicalNode(
            IndexQueryLogicalOperator.And, null!));
        Assert.Throws<ArgumentNullException>(() => IndexQueryParser.Unescape(null!));
    }
    #endregion
}
