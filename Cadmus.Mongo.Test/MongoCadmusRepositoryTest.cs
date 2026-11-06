using System;
using System.Text.Json;
using System.Threading.Tasks;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Cadmus.TestBase;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Xunit;

namespace Cadmus.Mongo.Test;

// https://github.com/xunit/xunit/issues/1999

[CollectionDefinition(nameof(NonParallelResourceCollection),
    DisableParallelization = true)]
public class NonParallelResourceCollection { }

[Collection(nameof(NonParallelResourceCollection))]
public class MongoCadmusRepositoryTest : CadmusRepositoryTestBase
{
    protected override ICadmusRepository GetRepository() =>
        TestDb.CreateRepository();

    protected override void PrepareDatabase() => TestDb.Prepare();

    #region Helper
    [Fact]
    public void GetDocumentsPage_NoFilterSortByKey_Ok()
    {
        PrepareDatabase();
        IMongoDatabase db = TestDb.GetDatabase();
        var collection = db.GetCollection<BsonDocument>(MongoItem.COLLECTION);

        var page = MongoHelper.GetDocumentsPage(collection,
            "{}", "{\"sortKey\":1}", 1, 10);

        Assert.Equal(10, page.Items.Count);
        Assert.Equal(20, page.Total);
    }

    [Fact]
    public void GetDocumentsPage_UserIdSortByKey_Ok()
    {
        PrepareDatabase();
        IMongoDatabase db = TestDb.GetDatabase();
        var collection = db.GetCollection<BsonDocument>(MongoItem.COLLECTION);

        var page = MongoHelper.GetDocumentsPage(collection,
            "{\"userId\": \"Odd\"}", "{\"sortKey\":1}", 1, 10);

        Assert.Equal(10, page.Items.Count);
        Assert.Equal(10, page.Total);
    }
    #endregion

    #region Flags
    [Fact]
    public void GetFlagDefinitions_Ok()
    {
        DoGetFlagDefinitions_Ok();
    }

    [Fact]
    public void GetFlagDefinition_NotExisting_Null()
    {
        DoGetFlagDefinition_NotExisting_Null();
    }

    [Fact]
    public void GetFlagDefinition_Existing_Ok()
    {
        DoGetFlagDefinition_Existing_Ok();
    }

    [Fact]
    public void AddFlagDefinition_Existing_Updated()
    {
        DoAddFlagDefinition_Existing_Updated();
    }

    [Fact]
    public void AddFlagDefinition_NotExisting_Added()
    {
        DoAddFlagDefinition_NotExisting_Added();
    }

    [Fact]
    public void DeleteFlagDefinition_NotExisting_Nope()
    {
        DoDeleteFlagDefinition_NotExisting_Nope();
    }

    [Fact]
    public void DeleteFlagDefinition_Existing_Deleted()
    {
        DoDeleteFlagDefinition_Existing_Deleted();
    }
    #endregion

    #region Thesauri
    [Fact]
    public void GetThesaurusIds_Ok()
    {
        DoGetThesaurusIds_Ok();
    }

    [Fact]
    public void GetThesauri_NoFilterPage1_Ok()
    {
        DoGetThesauri_NoFilterPage1_Ok();
    }

    [Fact]
    public void GetThesauri_NoFilterPage2_Ok()
    {
        DoGetThesauri_NoFilterPage2_Ok();
    }

    [Fact]
    public void GetThesauri_FilterById_Ok()
    {
        DoGetThesauri_FilterById_Ok();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void GetThesauri_FilterByAlias_Ok(bool? alias)
    {
        DoGetThesauri_FilterByAlias_Ok(alias);
    }

    [Fact]
    public void GetThesauri_FilterByLanguage_Ok()
    {
        DoGetThesauri_FilterByLanguage_Ok();
    }

    [Fact]
    public void GetThesaurus_NotExisting_Null()
    {
        DoGetThesaurus_NotExisting_Null();
    }

    [Fact]
    public void GetThesaurus_Existing_Ok()
    {
        DoGetThesaurus_Existing_Ok();
    }

    [Fact]
    public void AddThesaurus_NotExisting_Added()
    {
        DoAddThesaurus_NotExisting_Added();
    }

    [Fact]
    public void AddThesaurus_Existing_Updated()
    {
        DoAddThesaurus_Existing_Updated();
    }

    [Fact]
    public void DeleteThesaurus_NotExisting_Nope()
    {
        DoDeleteThesaurus_NotExisting_Nope();
    }

    [Fact]
    public void DeleteThesaurus_Existing_Deleted()
    {
        DoDeleteThesaurus_Existing_Deleted();
    }

    [Fact]
    public void GetThesaurusAliases_NoAliases_Empty()
    {
        DoGetThesaurusAliases_NoAliases_Empty();
    }

    [Fact]
    public void GetThesaurusAliases_WithAliases_Ok()
    {
        DoGetThesaurusAliases_WithAliases_Ok();
    }

    [Fact]
    public void GetThesaurusAliases_NoTarget_Empty()
    {
        DoGetThesaurusAliases_NoTarget_Empty();
    }
    #endregion

    #region Facets
    [Fact]
    public void GetFacets_Ok()
    {
        DoGetFacets_Ok();
    }

    [Fact]
    public void GetFacet_NotExisting_Null()
    {
        DoGetFacet_NotExisting_Null();
    }

    [Fact]
    public void GetFacet_Existing_Ok()
    {
        DoGetFacet_Existing_Ok();
    }

    [Fact]
    public void AddFacet_Existing_Updated()
    {
        DoAddFacet_Existing_Updated();
    }

    [Fact]
    public void AddFacet_NotExisting_Added()
    {
        DoAddFacet_NotExisting_Added();
    }

    [Fact]
    public void DeleteFacet_NotExisting_Nope()
    {
        DoDeleteFacet_NotExisting_Nope();
    }

    [Fact]
    public void DeleteFacet_Existing_Deleted()
    {
        DoDeleteFacet_Existing_Deleted();
    }
    #endregion

    #region Items
    [Fact]
    public void GetItemsPage_1_Ok()
    {
        DoGetItemsPage_1_Ok();
    }

    [Fact]
    public void GetItemsPage_2_Ok()
    {
        DoGetItemsPage_2_Ok();
    }

    [Fact]
    public void GetItemsPage_10_Empty()
    {
        DoGetItemsPage_10_Empty();
    }

    [Fact]
    public void GetItemsPage_Filtered_Ok()
    {
        DoGetItemsPage_Filtered_Ok();
    }

    [Fact]
    public void GetItem_NotExisting_Null()
    {
        DoGetItem_NotExisting_Null();
    }

    [Fact]
    public void GetItem_Existing_Ok()
    {
        DoGetItem_Existing_Ok();
    }

    [Fact]
    public void GetItem_ExistingExcludeParts_Ok()
    {
        DoGetItem_ExistingExcludeParts_Ok();
    }

    [Fact]
    public void AddItem_Existing_Updated()
    {
        DoAddItem_Existing_Updated();
    }

    [Fact]
    public void AddItem_NotExisting_Added()
    {
        DoAddItem_NotExisting_Added();
    }

    [Fact]
    public void SetItemFlags_NotExisting_Nope()
    {
        DoSetItemFlags_NotExisting_Nope();
    }

    [Fact]
    public void SetItemFlags_Existing_Updated()
    {
        DoSetItemFlags_Existing_Updated();
    }

    [Fact]
    public void SetItemGroupId_NotExisting_Nope()
    {
        DoSetItemGroupId_NotExisting_Nope();
    }

    [Fact]
    public void SetItemGroupId_Existing_Updated()
    {
        DoSetItemGroupId_Existing_Updated();
    }

    [Fact]
    public async Task GetDistinctGroupIdsAsync_All_11()
    {
        await DoGetDistinctGroupIdsAsync_All_11();
    }

    [Fact]
    public async Task GetDistinctGroupIdsAsync_Filtered_1()
    {
        await DoGetDistinctGroupIdsAsync_Filtered_1();
    }

    [Fact]
    public async Task GetDistinctGroupIdsAsync_Page1_5()
    {
        await DoGetDistinctGroupIdsAsync_Page1_5();
    }

    [Fact]
    public async Task GetDistinctGroupIdsAsync_Page2_5()
    {
        await DoGetDistinctGroupIdsAsync_Page2_5();
    }

    [Fact]
    public async Task GetGroupLayersCountAsync_NoItem_0()
    {
        await DoGetGroupLayersCountAsync_NoItem_0();
    }

    [Fact]
    public async Task GetGroupLayersCountAsync_Items_2()
    {
        await DoGetGroupLayersCountAsync_Items_2();
    }

    [Fact]
    public void DeleteItem_NotExisting_Nope()
    {
        DoDeleteItem_NotExisting_Nope();
    }

    [Fact]
    public void DeleteItem_Existing_Deleted()
    {
        DoDeleteItem_ExistingNoParts_Deleted();
    }

    [Fact]
    public void DeleteItem_ExistingNoPartsNoHistory_Deleted()
    {
        DoDeleteItem_ExistingNoPartsNoHistory_Deleted();
    }

    [Fact]
    public void DeleteItem_ExistingWithParts_Deleted()
    {
        DoDeleteItem_ExistingWithParts_Deleted();
    }

    [Fact]
    public void GetItemLayerInfo_NoAbsentNoItem_Empty()
    {
        DoGetItemLayerInfo_NoAbsentNoItem_Empty();
    }

    [Fact]
    public void GetItemLayerInfo_AbsentNoItem_Empty()
    {
        DoGetItemLayerInfo_AbsentNoItem_Empty();
    }

    [Fact]
    public void GetItemLayerInfo_ItemNoAbsentNoPart_0()
    {
        DoGetItemLayerInfo_ItemNoAbsentNoPart_0();
    }

    [Fact]
    public void GetItemLayerInfo_ItemAbsentNoPart_2()
    {
        DoGetItemLayerInfo_ItemAbsentNoPart_2();
    }

    [Fact]
    public void GetItemLayerInfo_ItemAbsent1Part_2()
    {
        DoGetItemLayerInfo_ItemAbsent1Part_2();
    }
    #endregion

    #region Export/Import
    [Fact]
    public void ExportItem_NotExisting_False()
    {
        DoExportItem_NotExisting_False();
    }

    [Fact]
    public void ExportItem_WithParts_Ok()
    {
        DoExportItem_WithParts_Ok();
    }

    [Fact]
    public void ExportItem_NoParts_Ok()
    {
        DoExportItem_NoParts_Ok();
    }

    [Fact]
    public void ImportItem_DeletedNoHistory_SameIds()
    {
        DoImportItem_DeletedNoHistory_SameIds();
    }

    [Fact]
    public void ImportItem_Deleted_NewIds()
    {
        DoImportItem_Deleted_NewIds();
    }

    [Fact]
    public void ImportItem_NoHistory_NoHistory()
    {
        DoImportItem_NoHistory_NoHistory();
    }

    [Fact]
    public void ImportItem_Existing_Throws()
    {
        DoImportItem_Existing_Throws();
    }

    [Fact]
    public void ImportItem_Clone_NewPartIds()
    {
        DoImportItem_Clone_NewPartIds();
    }

    [Fact]
    public void ImportItem_NoParts_Added()
    {
        DoImportItem_NoParts_Added();
    }

    [Theory]
    [InlineData("syntax")]
    [InlineData("not-object")]
    [InlineData("no-id")]
    [InlineData("parts-not-array")]
    [InlineData("part-no-type")]
    [InlineData("part-unknown-type")]
    [InlineData("part-other-item")]
    [InlineData("part-bad-content")]
    public void ImportItem_Invalid_Throws(string invalidCase)
    {
        DoImportItem_Invalid_Throws(invalidCase);
    }
    #endregion

    #region Parts
    [Fact]
    public void GetPartsPage_1Any_2()
    {
        DoGetPartsPage_1Any_2();
    }

    [Fact]
    public void GetPartsPage_1TypeId_1()
    {
        DoGetPartsPage_1TypeId_1();
    }

    [Fact]
    public void GetPartsPage_1RoleId_1()
    {
        DoGetPartsPage_1RoleId_1();
    }

    [Fact]
    public void GetPartsPage_TypeIdSortExpressions_Ok()
    {
        DoGetPartsPage_TypeIdSortExpressions_Ok();
    }

    [Fact]
    public void GetPartsPage_ItemIdTypeId_Ok()
    {
        DoGetPartsPage_ItemIdTypeId_Ok();
    }

    [Fact]
    public void GetItemParts_Item1_2()
    {
        DoGetItemParts_Item1_2();
    }

    [Fact]
    public void GetItemParts_Items1And2_2()
    {
        DoGetItemParts_Items1And2_2();
    }

    [Fact]
    public void GetPart_NotExisting_Null()
    {
        DoGetPart_NotExisting_Null();
    }

    [Fact]
    public void GetPartCreatorId_NotExisting_Null()
    {
        DoGetPartCreatorId_NotExisting_Null();
    }

    [Fact]
    public void GetPartCreatorId_Existing_Ok()
    {
        DoGetPartCreatorId_Existing_Ok();
    }

    [Fact]
    public void GetPartContent_NotExisting_Null()
    {
        DoGetPartContent_NotExisting_Null();
    }

    [Fact]
    public void GetPartContent_Existing_Ok()
    {
        DoGetPartContent_Existing_Ok();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddPart_NotExisting_Added(bool history)
    {
        DoAddPart_NotExisting_Added(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddPart_Existing_Updated(bool history)
    {
        DoAddPart_Existing_Updated(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddPartFromContent_NotExisting_Added(bool history)
    {
        DoAddPartFromContent_NotExisting_Added(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AddPartFromContent_Existing_Updated(bool history)
    {
        DoAddPartFromContent_Existing_Updated(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeletePart_NotExisting_Nope(bool history)
    {
        DoDeletePart_NotExisting_Nope(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void DeletePart_Existing_Deleted(bool history)
    {
        DoDeletePart_Existing_Deleted(history);
    }

    [Fact]
    public void GetLayerPartBreakChance_NoLayerPart_0()
    {
        DoGetLayerPartBreakChance_NoLayerPart_0();
    }

    [Fact]
    public void GetLayerPartBreakChance_NoFacet_1()
    {
        DoGetLayerPartBreakChance_NoFacet_1();
    }

    [Fact]
    public void GetLayerPartBreakChance_NoTextPart_2()
    {
        DoGetLayerPartBreakChance_NoTextPart_2();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetLayerPartBreakChance_TextSavedPastLayer_1(
        bool history)
    {
        DoGetLayerPartBreakChance_TextSavedPastLayer_1(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetLayerPartBreakChance_TextSavedPastLayerNoChange_0(
        bool history)
    {
        DoGetLayerPartBreakChance_TextSavedPastLayerNoChange_0(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetLayerPartBreakChance_TextSavedBeforeLayerNoTolerance_0(
        bool history)
    {
        DoGetLayerPartBreakChance_TextSavedBeforeLayerNoTolerance_0(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetLayerPartBreakChance_OriginalTextSavedBeforeLayerInInterval_01(
        bool history)
    {
        DoGetLayerPartBreakChance_OriginalTextSavedBeforeLayerInInterval_01(history);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GetLayerPartBreakChance_TextSavedBeforeLayerInInterval_1(
        bool history)
    {
        DoGetLayerPartBreakChance_TextSavedBeforeLayerInInterval_1(history);
    }

    [Fact]
    public void GetLayerHints_Unchanged_Ok()
    {
        DoGetLayerHints_Unchanged_Ok();
    }

    [Fact]
    public void GetLayerHints_Changed_Ok()
    {
        DoGetLayerHints_Changed_Ok();
    }

    [Fact]
    public void ApplyLayerPartPatches_Del_Ok()
    {
        DoApplyLayerPartPatches_Del_Ok();
    }

    [Fact]
    public void SetPartThesaurusScope_NoMatch_Unchanged()
    {
        DoSetPartThesaurusScope_NoMatch_Unchanged();
    }

    [Fact]
    public void SetPartThesaurusScope_Match_Updated()
    {
        DoSetPartThesaurusScope_Match_Updated();
    }
    #endregion

    #region Settings
    [Fact]
    public void AddSetting_ShouldAddOrUpdateSetting()
    {
        ICadmusRepository repository = GetRepository();
        const string key = "testKey";
        const string json = "{ \"name\": \"test\" }";

        repository.AddSetting(key, json);

        string? result = repository.GetSetting(key);

        // parse the JSON string
        Assert.NotNull(result);
        JsonElement root = JsonDocument.Parse(result).RootElement;

        // check for the presence and value of the "name" property
        Assert.True(root.TryGetProperty("name", out var nameProperty));
        Assert.Equal("test", nameProperty.GetString());
    }

    [Fact]
    public void GetSetting_ShouldReturnSetting_WhenFound()
    {
        ICadmusRepository repository = GetRepository();
        const string key = "testKey";
        const string json = "{ \"name\": \"test\", \"year\": 2025 }";
        repository.AddSetting(key, json);

        string? result = repository.GetSetting(key);

        // parse the JSON string
        Assert.NotNull(result);
        JsonElement root = JsonDocument.Parse(result).RootElement;

        // check for the presence and values of the "name" and "year" properties
        Assert.True(root.TryGetProperty("name", out var nameProperty));
        Assert.Equal("test", nameProperty.GetString());

        Assert.True(root.TryGetProperty("year", out var yearProperty));
        Assert.Equal(2025, yearProperty.GetInt32());
    }

    [Fact]
    public void GetSetting_ShouldReturnNull_WhenNotFound()
    {
        ICadmusRepository repository = GetRepository();
        const string key = "nonExistingKey";

        string? result = repository.GetSetting(key);

        Assert.Null(result);
    }

    [Fact]
    public void DeleteSetting_ShouldDeleteSetting()
    {
        ICadmusRepository repository = GetRepository();
        const string key = "testKey";
        const string json = "{ \"name\": \"test\" }";
        repository.AddSetting(key, json);

        repository.DeleteSetting(key);

        string? result = repository.GetSetting(key);
        Assert.Null(result);
    }

    [Fact]
    public void DeleteSetting_ShouldNotThrowWhenNotFound()
    {
        ICadmusRepository repository = GetRepository();

        Exception? exception = Record.Exception(() =>
            repository.DeleteSetting("nonExistingKey"));

        Assert.Null(exception);
    }
    #endregion
}
