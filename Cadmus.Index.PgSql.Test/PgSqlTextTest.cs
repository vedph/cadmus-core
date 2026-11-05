using System;
using Xunit;

namespace Cadmus.Index.PgSql.Test;

public sealed class PgSqlTextTest
{
    [Theory]
    [InlineData(null, 3, "")]
    [InlineData("", 3, "")]
    [InlineData("abc", 3, "abc")]
    [InlineData("abcd", 3, "abc")]
    [InlineData("a\0b\0c", 3, "abc")]
    [InlineData("ab\U0001D11E", 3, "ab")]
    [InlineData("a\U0001D11E", 3, "a\U0001D11E")]
    public void Fit_Ok(string? text, int max, string expected)
    {
        Assert.Equal(expected, PgSqlText.Fit(text, max));
    }

    [Fact]
    public void FitOrNull_Ok()
    {
        Assert.Null(PgSqlText.FitOrNull(null, 3));
        Assert.Equal("ab", PgSqlText.FitOrNull("abc", 2));
    }

    [Fact]
    public void ToUtc_Ok()
    {
        DateTime utc = new(2024, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        Assert.Equal(utc, PgSqlText.ToUtc(utc));

        DateTime unspecified = new(2024, 1, 1, 10, 0, 0);
        DateTime result = PgSqlText.ToUtc(unspecified);
        Assert.Equal(DateTimeKind.Utc, result.Kind);
        Assert.Equal(10, result.Hour);

        DateTime local = utc.ToLocalTime();
        Assert.Equal(utc, PgSqlText.ToUtc(local));
    }

    [Fact]
    public void QuoteIdentifier_Ok()
    {
        Assert.Equal("\"a\"\"b\"", PgSqlText.QuoteIdentifier("a\"b"));
    }

    [Fact]
    public void DataSources_NoConnectionString_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => PgSqlDataSources.Get(""));
    }

    [Fact]
    public void DataSources_SameConnectionString_SameSource()
    {
        string cs = TestHelper.GetConnectionString("x");
        Assert.Same(PgSqlDataSources.Get(cs), PgSqlDataSources.Get(cs));
    }
}
