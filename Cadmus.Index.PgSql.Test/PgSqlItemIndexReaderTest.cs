using Cadmus.Core;
using Cadmus.Core.Config;
using Fusi.Tools.Data;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.PgSql.Test;

/// <summary>
/// Reader tests. All the tests share the same dataset:
/// <list type="bullet">
/// <item><description>A: "Alpha one", group g1, flags 1, created 2024-01-01,
/// with a note part (tag=odd, text, year=1200, eid=x_y) and a categories
/// part with role fr.x (category=lit, a pin named "title").</description>
/// </item>
/// <item><description>B: "Beta two", no group, flags 6, created 2024-06-01,
/// with a note part (tag=even, year=950, year=1200).</description></item>
/// <item><description>C: "Gamma three", no group, flags 1, created
/// 2024-09-01, without parts, with a description with special characters.
/// </description></item>
/// </list>
/// </summary>
[Collection(nameof(DbCollection))]
public sealed class PgSqlItemIndexReaderTest : IAsyncLifetime
{
    private const string A = "00000000-0000-0000-0000-00000000000a";
    private const string B = "00000000-0000-0000-0000-00000000000b";
    private const string C = "00000000-0000-0000-0000-00000000000c";
    private const string PA1 = "10000000-0000-0000-0000-00000000000a";
    private const string PA2 = "20000000-0000-0000-0000-00000000000a";
    private const string PB1 = "10000000-0000-0000-0000-00000000000b";

    private static readonly FlagDefinition[] _flags =
    [
        new FlagDefinition { Id = 1, Label = "to revise" },
        new FlagDefinition { Id = 4, Label = "checked" },
    ];

    public async ValueTask InitializeAsync()
    {
        PgSqlItemIndexWriter writer = TestHelper.GetWriter();
        await writer.CreateIndexAsync();
        await writer.ClearAsync();

        Item a = TestHelper.GetItem(A, "Alpha one", "a", 1);
        a.GroupId = "g1";
        a.UserId = "hera";
        TestHelper.AddPart(a, PA1)
            .AddPin("tag", "odd")
            .AddPin("text", "a note for alpha")
            .AddPin("year", "1200")
            .AddPin("eid", "x_y");
        TestHelper.AddPart(a, PA2, "it.vedph.categories", "fr.x")
            .AddPin("category", "lit")
            .AddPin("title", "Pin Title");

        Item b = TestHelper.GetItem(B, "Beta two", "b", 6);
        b.TimeCreated = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        TestHelper.AddPart(b, PB1)
            .AddPin("tag", "even")
            .AddPin("year", "950")
            .AddPin("year", "1200");

        Item c = TestHelper.GetItem(C, "Gamma three", "c", 1);
        c.TimeCreated = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc);
        c.Description = @"Third item: 100% [brackets] back\slash 42";

        await writer.WriteItemsAsync([a, b, c]);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<string> SearchAsync(string query,
        PagingOptions? options = null)
    {
        DataPage<ItemInfo> page = await TestHelper.GetReader(flags: _flags)
            .SearchItemsAsync(query, options ?? new PagingOptions { PageSize = 0 });
        return string.Join(",", page.Items.Select(i => i.Id[^1]));
    }

    private static async Task<string> SearchPinsAsync(string query)
    {
        DataPage<DataPinInfo> page = await TestHelper.GetReader()
            .SearchPinsAsync(query, new PagingOptions { PageSize = 0 });
        Assert.Equal(page.Total, page.Items.Count);
        return string.Join(",", page.Items.Select(
            p => $"{p.ItemId![^1]}:{p.Name}={p.Value}"));
    }

    #region Arguments
    [Fact]
    public void Configure_Null_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new PgSqlItemIndexReader().Configure(null!));
        Assert.Throws<ArgumentNullException>(
            () => new PgSqlItemIndexReader().ConnectionString = null!);
    }

    [Fact]
    public async Task Search_NullArguments_Throw()
    {
        PgSqlItemIndexReader reader = TestHelper.GetReader();
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => reader.SearchItemsAsync(null!, new PagingOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => reader.SearchItemsAsync("x", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => reader.SearchPinsAsync(null!, new PagingOptions()));
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => reader.SearchPinsAsync("x", null!));
    }
    #endregion

    #region Items
    [Fact]
    public async Task SearchItems_PlainText_TitleContains()
    {
        Assert.Equal("a", await SearchAsync("pha o"));
    }

    [Fact]
    public async Task SearchItems_Page_Ok()
    {
        DataPage<ItemInfo> page = await TestHelper.GetReader()
            .SearchItemsAsync("[facet=default]",
                new PagingOptions { PageNumber = 2, PageSize = 2 });

        Assert.Equal(3, page.Total);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(2, page.PageSize);
        ItemInfo item = Assert.Single(page.Items);
        Assert.Equal(C, item.Id);
    }

    [Fact]
    public async Task SearchItems_Fields_Ok()
    {
        DataPage<ItemInfo> page = await TestHelper.GetReader()
            .SearchItemsAsync("[title=Alpha one]", new PagingOptions());

        ItemInfo item = Assert.Single(page.Items);
        Assert.Equal(A, item.Id);
        Assert.Equal("Alpha one", item.Title);
        Assert.Equal("Description of Alpha one", item.Description);
        Assert.Equal("default", item.FacetId);
        Assert.Equal("g1", item.GroupId);
        Assert.Equal("a", item.SortKey);
        Assert.Equal(1, item.Flags);
        Assert.Equal("zeus", item.CreatorId);
        Assert.Equal("hera", item.UserId);
        Assert.Equal(new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            item.TimeCreated);
        Assert.Equal(new DateTime(2024, 1, 2, 0, 0, 0, DateTimeKind.Utc),
            item.TimeModified);
    }

    [Fact]
    public async Task SearchItems_NullableFields_Empty()
    {
        PgSqlItemIndexWriter writer = TestHelper.GetWriter();
        Item d = TestHelper.GetItem("d", "Delta", "d");
        d.CreatorId = null!;
        d.UserId = null!;
        await writer.WriteItemAsync(d);

        DataPage<ItemInfo> page = await TestHelper.GetReader()
            .SearchItemsAsync("[title=Delta]", new PagingOptions());

        ItemInfo item = Assert.Single(page.Items);
        Assert.Null(item.GroupId);
        Assert.Equal("", item.CreatorId);
        Assert.Equal("", item.UserId);
    }

    [Theory]
    // item fields
    [InlineData("[title=Alpha one]", "a")]
    [InlineData("[title=alpha one]", "")]
    [InlineData("[t*=a tw]", "b")]
    [InlineData("[title^=Gam]", "c")]
    [InlineData("[title$=two]", "b")]
    [InlineData("[title?=?eta*]", "b")]
    [InlineData("[title?=Beta two]", "b")]
    [InlineData("[title<>Beta two]", "a,c")]
    [InlineData("[itemId=" + C + "]", "c")]
    [InlineData("[sortKey=b]", "b")]
    [InlineData("[facet=default]", "a,b,c")]
    [InlineData("[creatorId=zeus]", "a,b,c")]
    [InlineData("[userId=hera]", "a")]
    [InlineData("[userId<>hera]", "b,c")]
    [InlineData("[group=g1]", "a")]
    [InlineData("[group=]", "b,c")]
    [InlineData("[group<>g1]", "b,c")]
    [InlineData("[group<>]", "a")]
    // special characters, as literals
    [InlineData("[dsc*=100%]", "c")]
    [InlineData("[dsc*=0\\%]", "")]
    [InlineData(@"[dsc*=\[brackets\]]", "c")]
    [InlineData(@"[dsc*=back\\slash]", "c")]
    [InlineData("[dsc*=: 1]", "c")]
    // time
    [InlineData("[timeCreated<2024-03-01]", "a")]
    [InlineData("[timeCreated>=2024-06-01]", "b,c")]
    [InlineData("[timeCreated=2024-06-01T00:00:00Z]", "b")]
    [InlineData("[timeModified>2024-01-01]", "a,b,c")]
    [InlineData("[time>2000-01-01]", "a,b")]
    public async Task SearchItems_ItemFields_Ok(string query, string expected)
    {
        Assert.Equal(expected, await SearchAsync(query));
    }

    [Theory]
    // regression: items without pins were never found
    [InlineData("[flags:1]", "a,c")]
    [InlineData("[flags:to revise]", "a,c")]
    [InlineData("[flags:to revise,checked]", "a,b,c")]
    [InlineData("[flags&:6]", "b")]
    [InlineData("[flags&:2,checked]", "b")]
    [InlineData("[flags!:1]", "b")]
    [InlineData("[flags=6]", "b")]
    [InlineData("[flags<>6]", "a,c")]
    [InlineData("[flags>=2]", "b")]
    [InlineData("[flags==1]", "a,c")]
    public async Task SearchItems_Flags_Ok(string query, string expected)
    {
        Assert.Equal(expected, await SearchAsync(query));
    }

    [Theory]
    [InlineData("[tag=odd]", "a")]
    // regression: clauses on different pins never matched
    [InlineData("[tag=even] AND [year=950]", "b")]
    [InlineData("[tag=odd] AND [year=950]", "")]
    [InlineData("[tag=odd] AND [category=lit]", "a")]
    [InlineData("[tag=odd] OR [tag=even]", "a,b")]
    [InlineData("NOT [tag=odd]", "b,c")]
    [InlineData("[title*=a] AND NOT ([tag=odd] OR [tag=even])", "c")]
    [InlineData("[tag=odd] [title=Alpha one]", "a")]
    // regression: underscores were escaped also in equality
    [InlineData("[eid=x_y]", "a")]
    [InlineData("[eid^=x_]", "a")]
    [InlineData("[eid^=x%]", "")]
    [InlineData("[eid?=x?y]", "a")]
    [InlineData("[eid?=x*]", "a")]
    [InlineData("[eid$=_y]", "a")]
    [InlineData("[text*=note]", "a")]
    [InlineData("[tag<>odd]", "b")]
    // pin fields
    [InlineData("[type=it.vedph.categories]", "a")]
    [InlineData("[type=it.vedph.note]", "a,b")]
    [InlineData("[role=fr.x]", "a")]
    [InlineData("[partId=" + PB1 + "]", "b")]
    [InlineData("[name=category]", "a")]
    [InlineData("[value=lit]", "a")]
    [InlineData("[n=year] AND [v=950]", "b")]
    // pin named as a field alias
    [InlineData("[@title=Pin Title]", "a")]
    [InlineData("[title=Pin Title]", "")]
    // numeric
    [InlineData("[year==1200]", "a,b")]
    [InlineData("[year==1200.0]", "a,b")]
    [InlineData("[year<1000]", "b")]
    [InlineData("[year<=950]", "b")]
    [InlineData("[year>1000]", "a,b")]
    [InlineData("[year>=1200]", "a,b")]
    [InlineData("[year!=950]", "a,b")]
    [InlineData("[tag==1]", "")]
    public async Task SearchItems_Pins_Ok(string query, string expected)
    {
        Assert.Equal(expected, await SearchAsync(query));
    }

    [Theory]
    [InlineData(@"[title~=^\[AB\]]", "a,b")]
    [InlineData("[title~=one|two]", "a,b")]
    // regression: backslashes were doubled
    [InlineData(@"[dsc~=\d+%]", "c")]
    [InlineData(@"[dsc~=back\\\\slash]", "c")]
    [InlineData("[tag~=^o]", "a")]
    public async Task SearchItems_Regex_Ok(string query, string expected)
    {
        Assert.Equal(expected, await SearchAsync(query));
    }

    [Theory]
    [InlineData("[title%=Alpa one:0.3]", "a")]
    [InlineData("[title%=Alpa one]", "")]
    [InlineData("[title%=Alpa one:0.3] OR [title%=Beta tvo:0.2]", "a,b")]
    [InlineData("[text%=a note for alfa:0.5]", "a")]
    public async Task SearchItems_Similar_Ok(string query, string expected)
    {
        Assert.Equal(expected, await SearchAsync(query));
    }

    [Theory]
    // invalid syntax, including injection attempts
    [InlineData("[title=x] OR 1=1")]
    [InlineData("[title=x]) UNION SELECT 1 --")]
    [InlineData("[title=x] OR (0 < (SELECT count(*) FROM pg_user))")]
    [InlineData("[title=x]; DROP TABLE pin")]
    // invalid regex detected by the database
    [InlineData("[title~=(]")]
    // invalid values
    [InlineData("[year==x]")]
    [InlineData("[flags:unknown]")]
    public async Task SearchItems_Invalid_Throws(string query)
    {
        await Assert.ThrowsAsync<CadmusQueryException>(() =>
            TestHelper.GetReader().SearchItemsAsync(query, new PagingOptions()));
    }

    [Fact]
    public async Task SearchItems_Injection_ValuesAreLiterals()
    {
        Assert.Equal("", await SearchAsync("[title=x' OR '1'='1]"));
        Assert.Equal(3, await TestHelper.CountAsync("SELECT COUNT(*) FROM item"));
    }

    [Fact]
    public async Task SearchItems_Cancelled_Throws()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            TestHelper.GetReader().SearchItemsAsync("[t=x]",
                new PagingOptions(), cts.Token));
    }

    [Fact]
    public async Task SearchItems_Timeout_Throws()
    {
        // another transaction locks the item table beyond the timeout
        await using Npgsql.NpgsqlConnection locker = new(
            TestHelper.GetConnectionString());
        await locker.OpenAsync();
        await using Npgsql.NpgsqlTransaction tr = await locker.BeginTransactionAsync();
        await using (Npgsql.NpgsqlCommand cmd = new(
            "LOCK TABLE item IN ACCESS EXCLUSIVE MODE", locker, tr))
        {
            await cmd.ExecuteNonQueryAsync();
        }

        PgSqlItemIndexReader reader = new();
        reader.Configure(new PgSqlIndexOptions
        {
            ConnectionString = TestHelper.GetConnectionString(),
            CommandTimeout = 1
        });
        CadmusQueryException ex = await Assert.ThrowsAsync<CadmusQueryException>(
            () => reader.SearchItemsAsync("[t=x]", new PagingOptions()));
        Assert.Contains("too long", ex.Message);

        await tr.RollbackAsync();
    }

    [Fact]
    public async Task SearchItems_SetFlagDefinitions_Ok()
    {
        PgSqlItemIndexReader reader = TestHelper.GetReader();
        await Assert.ThrowsAsync<CadmusQueryException>(() =>
            reader.SearchItemsAsync("[flags:checked]", new PagingOptions()));

        reader.SetFlagDefinitions(_flags);
        DataPage<ItemInfo> page = await reader.SearchItemsAsync(
            "[flags:checked]", new PagingOptions());
        Assert.Equal(B, Assert.Single(page.Items).Id);
    }
    #endregion

    #region Pins
    [Fact]
    public async Task SearchPins_Fields_Ok()
    {
        DataPage<DataPinInfo> page = await TestHelper.GetReader()
            .SearchPinsAsync("[category=lit]", new PagingOptions());

        DataPinInfo pin = Assert.Single(page.Items);
        Assert.Equal(A, pin.ItemId);
        Assert.Equal(PA2, pin.PartId);
        Assert.Equal("it.vedph.categories", pin.PartTypeId);
        Assert.Equal("fr.x", pin.RoleId);
        Assert.Equal("category", pin.Name);
        Assert.Equal("lit", pin.Value);
    }

    [Theory]
    [InlineData("[tag=odd]", "a:tag=odd")]
    // per-pin semantics: all the clauses apply to the same pin
    [InlineData("[tag=odd] AND [year=1200]", "")]
    [InlineData("[tag=odd] OR [year=1200]", "a:tag=odd,a:year=1200,b:year=1200")]
    // the lookup query used by the editor
    [InlineData("[partTypeId=it.vedph.note] AND [name=year] AND [value^=1]",
        "a:year=1200,b:year=1200")]
    [InlineData("[itemId=" + B + "]", "b:tag=even,b:year=1200,b:year=950")]
    [InlineData("[year==1200]", "a:year=1200,b:year=1200")]
    [InlineData("[name=tag] AND [title*=Beta]", "b:tag=even")]
    [InlineData("[name=tag] AND [flags!:1]", "b:tag=even")]
    [InlineData("[role=fr.x] AND NOT [name=title]", "a:category=lit")]
    [InlineData("[v%=a note for alfa:0.5]", "a:text=a note for alpha")]
    public async Task SearchPins_Ok(string query, string expected)
    {
        Assert.Equal(expected, await SearchPinsAsync(query));
    }

    [Fact]
    public async Task SearchPins_Page_Ok()
    {
        DataPage<DataPinInfo> page = await TestHelper.GetReader()
            .SearchPinsAsync("[name=year]",
                new PagingOptions { PageNumber = 2, PageSize = 2 });

        Assert.Equal(3, page.Total);
        DataPinInfo pin = Assert.Single(page.Items);
        Assert.Equal("950", pin.Value);
    }

    [Fact]
    public async Task SearchPins_Invalid_Throws()
    {
        await Assert.ThrowsAsync<CadmusQueryException>(() =>
            TestHelper.GetReader().SearchPinsAsync("[v~=(]", new PagingOptions()));
    }
    #endregion
}
