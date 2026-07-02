using Cadmus.Core.Storage;
using MongoDB.Bson;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cadmus.Export.Test;

// basic dataset ([C]reated, [U]pdated, [D]eleted):
// | obj     | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 |
// | ------- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
// | i1      | CU    |       |       |       |       |       |       |       |
// | i2      |       |       | CU    |       | U     |       |       |       |
// | i3      |       |       |       |       |       |       | CU    |       |
// | i4      |       | CU    |       |       |       |       |       | D     |
// | p1 (i1) | CU    |       |       |       |       |       |       |       |
// | p2 (i2) |       |       | CU    |       |       |       |       |       |
// | p3 (i2) |       |       |       | CU    |       | U     |       |       |
// | p4 (i3) |       |       |       |       |       |       | CU    |       |
// | p5 (i4) |       | CU    |       |       |       |       |       | D     |


// incremental dataset ([C]reated, [U]pdated, [D]eleted):
// | obj     | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 | 05-10 | 05-15 |
// | ------- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
// | i1      | CU    |       |       |       |       |       |       |       |       |       |
// | i2      |       |       | CU    |       | U     |       |       |       |       |       |
// | i3      |       |       |       |       |       |       | CU    |       |       |       |
// | i4      |       | CU    |       |       |       |       |       | D     |       |       |
// | i5      |       |       |       |       |       |       |       |       |       | CU    |
// | p1 (i1) | CU    |       |       |       |       |       |       |       |       |       |
// | p2 (i2) |       |       | CU    |       |       |       |       |       |       |       |
// | p3 (i2) |       |       |       | CU    |       | U     |       |       | U     |       |
// | p4 (i3) |       |       |       |       |       |       | CU    |       |       |       |
// | p5 (i4) |       | CU    |       |       |       |       |       | D     |       |       |
// | p6 (i5) |       |       |       |       |       |       |       |       |       | CU    |

// https://github.com/xunit/xunit/issues/1999
[CollectionDefinition(nameof(NonParallelResourceCollection),
    DisableParallelization = true)]

[Collection(nameof(NonParallelResourceCollection))]
public sealed class CadmusMongoDataFramerTest(MongoFixture fixture) :
    IClassFixture<MongoFixture>
{
    private readonly MongoFixture _fixture = fixture;

    private static CadmusJsonDumperOptions GetBasicOptions(
        string dbName = "test-db")
    {
        return new CadmusJsonDumperOptions
        {
            ConnectionString = "mongodb://localhost:27017/{0}",
            DatabaseName = dbName,
            OutputDirectory = Path.GetTempPath(),
            Indented = true
        };
    }

    private void LoadMockData(string resourceName)
    {
        Assembly assembly = typeof(CadmusMongoDataFramerTest).Assembly;
        using Stream? stream = assembly.GetManifestResourceStream(
            $"Cadmus.Export.Test.Assets.{resourceName}")
            ?? throw new ArgumentException($"Resource {resourceName} not found");
        _fixture.ClearDatabase();
        _fixture.LoadDataFromCsv(stream);
    }

    [Fact]
    public void GetItems_FullDump_ReturnsAllItems()
    {
        LoadMockData("BasicDataset.csv");

        // | obj     | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 |
        // | ------- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
        // | i1*     | CU    |       |       |       |       |       |       |       |
        // | i2*     |       |       | CU    |       | U     |       |       |       |
        // | i3*     |       |       |       |       |       |       | CU    |       |
        // | i4*     |       | CU    |       |       |       |       |       | D     |
        // | p1*(i1) | CU    |       |       |       |       |       |       |       |
        // | p2*(i2) |       |       | CU    |       |       |       |       |       |
        // | p3*(i2) |       |       |       | CU    |       | U     |       |       |
        // | p4*(i3) |       |       |       |       |       |       | CU    |       |
        // | p5*(i4) |       | CU    |       |       |       |       |       | D     |

        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        // create an empty filter for full dump
        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0  // no paging
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        Assert.Equal(4, items.Count); // should return all 4 items from history

        // verify we got the latest version of each item
        BsonDocument item1 = items.First(i => i["_id"].AsString == "item1");
        BsonDocument item2 = items.First(i => i["_id"].AsString == "item2");
        BsonDocument item3 = items.First(i => i["_id"].AsString == "item3");
        BsonDocument item4 = items.First(i => i["_id"].AsString == "item4");

        // check that statuses are correct
        Assert.Equal(EditStatus.Created, (EditStatus)item1["_status"].AsInt32);
        Assert.Equal(EditStatus.Updated, (EditStatus)item2["_status"].AsInt32);
        Assert.Equal(EditStatus.Created, (EditStatus)item3["_status"].AsInt32);
        Assert.Equal(EditStatus.Deleted, (EditStatus)item4["_status"].AsInt32);

        // check parts were correctly added
        Assert.True(item1.Contains("_parts"));
        Assert.Single(item1["_parts"].AsBsonArray);

        Assert.True(item2.Contains("_parts"));
        Assert.Equal(2, item2["_parts"].AsBsonArray.Count);

        Assert.True(item3.Contains("_parts"));
        Assert.Single(item3["_parts"].AsBsonArray);

        // deleted items should also have parts if they exist in history-parts
        Assert.True(item4.Contains("_parts"));
        Assert.Single(item4["_parts"].AsBsonArray);
    }

    [Fact]
    public void GetItems_NoParts_ReturnsItemsWithoutParts()
    {
        LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetBasicOptions();
        options.NoParts = true;
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        Assert.Equal(4, items.Count);

        // verify no parts were added
        foreach (BsonDocument item in items)
            Assert.False(item.Contains("_parts"));
    }

    [Fact]
    public void GetItems_NoDeleted_ExcludesDeletedItems()
    {
        LoadMockData("BasicDataset.csv");

        // | obj     | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 |
        // | ------- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
        // | i1*     | CU    |       |       |       |       |       |       |       |
        // | i2*     |       |       | CU    |       | U     |       |       |       |
        // | i3*     |       |       |       |       |       |       | CU    |       |
        // | i4      |       | CU    |       |       |       |       |       | D     |
        // | p1*(i1) | CU    |       |       |       |       |       |       |       |
        // | p2*(i2) |       |       | CU    |       |       |       |       |       |
        // | p3*(i2) |       |       |       | CU    |       | U     |       |       |
        // | p4*(i3) |       |       |       |       |       |       | CU    |       |
        // | p5 (i4) |       | CU    |       |       |       |       |       | D     |

        CadmusJsonDumperOptions options = GetBasicOptions();
        options.NoDeleted = true;
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, i => i["_id"].AsString == "item4");
    }

    [Fact]
    public void GetItems_FacetFilter_ReturnsMatchingItems()
    {
        LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            FacetId = "default",
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        // 2 active items + 1 deleted item with facetId=default
        Assert.Equal(3, items.Count);

        // item3 has facetId=special
        Assert.DoesNotContain(items, i => i["_id"].AsString == "item3");
    }

    [Fact]
    public void GetItems_TimeframeFilter_ReturnsItemsUpToTimeframe()
    {
        LoadMockData("BasicDataset.csv");

        // | obj      | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 |
        // | ---------| ----- | ----- | ----- | ----- | ===== | ----- | ----- | ----- |
        // | i1*      | CU    |       |       |       |       |       |       |       |
        // | i2*      |       |       | CU    |       | U     |       |       |       |
        // | i3       |       |       |       |       |       |       | CU    |       |
        // | i4*      |       | CU    |       |       |       |       |       | D     |
        // | p1* (i1) | CU    |       |       |       |       |       |       |       |
        // | p2* (i2) |       |       | CU    |       |       |       |       |       |
        // | p3* (i2) |       |       |       | CU    |       | U     |       |       |
        // | p4  (i3) |       |       |       |       |       |       | CU    |       |
        // | p5  (i4) |       | CU    |       |       |       |       |       | D     |

        // Set timeframe to 2023-03-01 - this should include:
        // - item1 (created 01-01), with part1
        // - item2 (updated 03-01), with part2 and part3 (before U on 03-15)
        // - item4 (deleted 05-01), with part5
        // But exclude:
        // - item3 (created 04-01)
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            MaxModified = new DateTime(2023, 3, 1, 23, 59, 59, DateTimeKind.Utc),
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        Assert.Equal(3, items.Count);
        Assert.Contains(items, i => i["_id"].AsString == "item1");
        Assert.Contains(items, i => i["_id"].AsString == "item2");
        Assert.Contains(items, i => i["_id"].AsString == "item4");
        Assert.DoesNotContain(items, i => i["_id"].AsString == "item3");

        // check that statuses are correct
        BsonDocument item1 = items.First(i => i["_id"].AsString == "item1");
        BsonDocument item2 = items.First(i => i["_id"].AsString == "item2");
        BsonDocument item4 = items.First(i => i["_id"].AsString == "item4");

        Assert.Equal(EditStatus.Created, (EditStatus)item1["_status"].AsInt32);
        Assert.Equal(EditStatus.Updated, (EditStatus)item2["_status"].AsInt32);
        Assert.Equal(EditStatus.Created, (EditStatus)item4["_status"].AsInt32);

        // check part content - for item2, we should get the version as of March 1
        BsonArray item2Parts = item2["_parts"].AsBsonArray;
        BsonValue? part3 = item2Parts.FirstOrDefault(
            p => p["_id"].AsString == "part3");
        Assert.NotNull(part3);

        // in our test data, part3 was updated multiple times:
        // we should get the version that was current as of March 1
        Assert.Equal("initial content3", part3["content"]["value"].AsString);
    }

    [Fact]
    public void GetItems_IncrementalTimeframeFilter_ReturnsItemsChangedInTimeframe()
    {
        LoadMockData("IncrementalDataset.csv");

        // | obj     | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 | 05-10 | 05-15 |
        // | ------- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
        // | i1      | CU    |       |       |       |       |       |       |       |       |       |
        // | i2*     |       |       | CU    |       | U     |       |       |       |       |       |
        // | i3      |       |       |       |       |       |       | CU    |       |       |       |
        // | i4*     |       | CU    |       |       |       |       |       | D     |       |       |
        // | i5*     |       |       |       |       |       |       |       |       |       | CU    |
        // | p1 (i1) | CU    |       |       |       |       |       |       |       |       |       |
        // | p2*(i2) |       |       | CU    |       |       |       |       |       |       |       |
        // | p3*(i2) |       |       |       | CU    |       | U     |       |       | U     |       |
        // | p4 (i3) |       |       |       |       |       |       | CU    |       |       |       |
        // | p5*(i4) |       | CU    |       |       |       |       |       | D     |       |       |
        // | p6*(i5) |       |       |       |       |       |       |       |       |       | CU    |

        // get items changed between 04-15 and 05-20, 2023.
        // This should include:
        // - item4 (deleted on May 1)
        // - item5 (created on May 15)
        // - item2 (has part3 updated on May 10)
        CadmusJsonDumperOptions options = GetBasicOptions();
        options.IsIncremental = true;
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            MinModified = new DateTime(2023, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxModified = new DateTime(2023, 5, 20, 23, 59, 59, DateTimeKind.Utc),
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        Assert.Equal(3, items.Count);
        // deleted May 1
        Assert.Contains(items, i => i["_id"].AsString == "item4");
        // created May 15
        Assert.Contains(items, i => i["_id"].AsString == "item5");
        // has part updated May 10
        Assert.Contains(items, i => i["_id"].AsString == "item2");

        // check that part3 of item2 has the latest content as of May 20
        BsonDocument item2 = items.First(i => i["_id"].AsString == "item2");
        BsonArray parts = item2["_parts"].AsBsonArray;
        BsonValue? part3 = parts.FirstOrDefault(p => p["_id"].AsString == "part3");

        Assert.NotNull(part3);
        Assert.Equal("updated content3", part3["content"]["value"].AsString);
    }

    [Fact]
    public void GetItems_NoPartDate_ExcludesItemsWithMatchingPartsOnly()
    {
        LoadMockData("IncrementalDataset.csv");

        // Get items changed between April 15 and May 20, 2023.
        // With NoPartDate, item2 should be excluded because the item itself
        // wasn't modified in the timeframe: only its part was
        CadmusJsonDumperOptions options = GetBasicOptions();
        options.NoPartDate = true;
        options.IsIncremental = true;
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            MinModified = new DateTime(2023, 4, 15, 0, 0, 0, DateTimeKind.Utc),
            MaxModified = new DateTime(2023, 5, 20, 23, 59, 59, DateTimeKind.Utc),
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        Assert.Equal(2, items.Count);
        // deleted May 1
        Assert.Contains(items, i => i["_id"].AsString == "item4");
        // created May 15
        Assert.Contains(items, i => i["_id"].AsString == "item5");
        // only part updated, not item
        Assert.DoesNotContain(items, i => i["_id"].AsString == "item2");
    }

    [Fact]
    public void GetItems_WhitelistPartTypes_FiltersPartsByType()
    {
        LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0,
            WhitePartTypeKeys = ["token"]
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        foreach (BsonDocument? item in items.Where(i => i.Contains("_parts")
            && i["_parts"].AsBsonArray.Count > 0))
        {
            BsonArray parts = item["_parts"].AsBsonArray;
            Assert.All(parts, p => Assert.Equal("token", p["typeId"].AsString));
        }
    }

    [Fact]
    public void GetItems_BlacklistPartTypes_ExcludesPartsByType()
    {
        LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0,
            BlackPartTypeKeys = ["token"]
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        foreach (BsonDocument? item in items.Where(i => i.Contains("_parts")
            && i["_parts"].AsBsonArray.Count > 0))
        {
            BsonArray parts = item["_parts"].AsBsonArray;
            Assert.All(parts, p => Assert.NotEqual("token", p["typeId"].AsString));
        }
    }

    [Fact]
    public void GetItems_RoleFilter_FiltersPartsByRole()
    {
        LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0,
            WhitePartTypeKeys = ["token:sample"]
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        // find all items that have parts
        List<BsonDocument> itemsWithParts = [..
            items.Where(i => i.Contains("_parts") &&
                             i["_parts"].AsBsonArray.Count > 0)];

        // check if any item has a part with typeId=token and roleId=sample
        bool anyMatchingPart = false;
        foreach (BsonDocument? item in itemsWithParts)
        {
            BsonArray parts = item["_parts"].AsBsonArray;
            foreach (BsonValue part in parts)
            {
                if (part["typeId"].AsString == "token" &&
                    part.AsBsonDocument.Contains("roleId") &&
                    part["roleId"].AsString == "sample")
                {
                    anyMatchingPart = true;
                }
            }
        }

        Assert.True(anyMatchingPart);
    }

    [Fact]
    public void GetItems_Pagination_ReturnsCorrectPage()
    {
        LoadMockData("BasicDataset.csv");

        // create dumper
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        // configure to return 2 items per page
        CadmusDumpFilter page1Filter = new()
        {
            PageNumber = 1,
            PageSize = 2
        };

        // get page 1
        List<BsonDocument> page1Items = [.. dumper.GetItems(page1Filter)];

        // get page 2
        CadmusDumpFilter page2Filter = new()
        {
            PageNumber = 2,
            PageSize = 2
        };
        List<BsonDocument> page2Items = [.. dumper.GetItems(page2Filter)];

        Assert.Equal(2, page1Items.Count);
        Assert.Equal(2, page2Items.Count);

        // make sure we got different items on different pages
        List<string> allItemIds = page1Items.Select(i => i["_id"].AsString)
            .Concat(page2Items.Select(i => i["_id"].AsString))
            .ToList();

        Assert.Equal(4, allItemIds.Distinct().Count());
    }

    [Fact]
    public void GetItems_ReuseDumper_WorksWithMultipleCalls()
    {
        LoadMockData("BasicDataset.csv");

        // create dumper with basic options
        CadmusJsonDumperOptions options = GetBasicOptions();
        CadmusMongoDataFramer dumper = new(options);

        // first call - get all items
        CadmusDumpFilter allItemsFilter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };
        List<BsonDocument> allItems = [.. dumper.GetItems(allItemsFilter)];

        // second call - get only default facet items
        CadmusDumpFilter defaultFacetFilter = new()
        {
            FacetId = "default",
            PageNumber = 1,
            PageSize = 0
        };
        List<BsonDocument> defaultFacetItems =
            [.. dumper.GetItems(defaultFacetFilter)];

        // Third call - get only special facet items
        CadmusDumpFilter specialFacetFilter = new()
        {
            FacetId = "special",
            PageNumber = 1,
            PageSize = 0
        };
        List<BsonDocument> specialFacetItems =
            [.. dumper.GetItems(specialFacetFilter)];

        Assert.Equal(4, allItems.Count);
        Assert.Equal(3, defaultFacetItems.Count);
        Assert.Single(specialFacetItems);

        // verify we get correct items for each filter
        Assert.Contains(defaultFacetItems, i => i["_id"].AsString == "item1");
        Assert.Contains(defaultFacetItems, i => i["_id"].AsString == "item2");
        Assert.Contains(defaultFacetItems, i => i["_id"].AsString == "item4");
        Assert.DoesNotContain(defaultFacetItems, i => i["_id"].AsString == "item3");

        Assert.Contains(specialFacetItems, i => i["_id"].AsString == "item3");
    }

    [Fact]
    public void GetItems_ItemIncludedByPartChange_StatusChangedToUpdated()
    {
        LoadMockData("IncrementalDataset.csv");

        // | obj     | 01-01 | 01-15 | 02-01 | 02-02 | 03-01 | 03-15 | 04-01 | 05-01 | 05-10 | 05-15 |
        // | ------- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- | ----- |
        // | i1      | CU    |       |       |       |       |       |       |       |       |       |
        // | i2*     |       |       | CU    |       | U     |       |       |       |       |       |
        // | i3*     |       |       |       |       |       |       | CU    |       |       |       |
        // | i4      |       | CU    |       |       |       |       |       | D     |       |       |
        // | i5      |       |       |       |       |       |       |       |       |       | CU    |
        // | p1 (i1) | CU    |       |       |       |       |       |       |       |       |       |
        // | p2 (i2) |       |       | CU    |       |       |       |       |       |       |       |
        // | p3 (i2) |       |       |       | CU    |       | U     |       |       | U     |       |
        // | p4 (i3) |       |       |       |       |       |       | CU    |       |       |       |
        // | p5 (i4) |       | CU    |       |       |       |       |       | D     |       |       |
        // | p6 (i5) |       |       |       |       |       |       |       |       |       | CU    |

        // Focus on item3, which was Created on 04-01 with part4
        // Then we look at a timeframe of 05-05 to 05-20 where only part3 of
        // item2 was updated.
        // Since item2 itself wasn't changed in this window, but its part was, 
        // it should have its status changed to Updated instead of remaining
        // Created.

        CadmusJsonDumperOptions options = GetBasicOptions();
        options.IsIncremental = true;
        CadmusMongoDataFramer dumper = new(options);

        // define a timeframe that includes the part update on 05-10
        // but not the last item2 update on 03-01
        CadmusDumpFilter filter = new()
        {
            MinModified = new DateTime(2023, 5, 5, 0, 0, 0, DateTimeKind.Utc),
            MaxModified = new DateTime(2023, 5, 20, 23, 59, 59, DateTimeKind.Utc),
            PageNumber = 1,
            PageSize = 0
        };

        List<BsonDocument> items = [.. dumper.GetItems(filter)];

        // verify that item2 is included in the results (due to its part3
        // being updated)
        BsonDocument? item2 = items.FirstOrDefault(i => i["_id"].AsString == "item2");
        Assert.NotNull(item2);

        // verify that the status was changed from Created to Updated
        Assert.Equal(EditStatus.Updated, (EditStatus)item2["_status"].AsInt32);

        // verify item5 was created in this timeframe and has the correct status
        BsonDocument? item5 = items.FirstOrDefault(i => i["_id"].AsString == "item5");
        Assert.NotNull(item5);
        Assert.Equal(EditStatus.Created, (EditStatus)item5["_status"].AsInt32);

        // check that we got the right version of part3 from the update on 05-10
        if (!options.NoParts)
        {
            BsonArray parts = item2["_parts"].AsBsonArray;
            BsonValue? part3 = parts.FirstOrDefault(p => p["_id"].AsString == "part3");
            Assert.NotNull(part3);
            Assert.Equal("updated content3", part3["content"]["value"].AsString);
        }
    }
}
