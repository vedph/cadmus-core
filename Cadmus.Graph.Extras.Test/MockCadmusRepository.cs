using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Layers;
using Cadmus.Core.Storage;
using Fusi.Tools.Data;

namespace Cadmus.Graph.Extras.Test;

/// <summary>
/// Minimal <see cref="ICadmusRepository"/> test double. Only
/// <see cref="GetItemParts"/> is meaningfully implemented (configurable via
/// <see cref="ItemParts"/>); every other member throws
/// <see cref="NotImplementedException"/> since it is not expected to be
/// invoked by the code under test.
/// </summary>
internal sealed class MockCadmusRepository : ICadmusRepository
{
    public IList<IPart> ItemParts { get; set; } = [];

    public IList<IPart> GetItemParts(string[] itemIds, string? typeId = null,
        string? roleId = null) => ItemParts;

    #region Not implemented
    public IList<FlagDefinition> GetFlagDefinitions() =>
        throw new NotImplementedException();
    public FlagDefinition? GetFlagDefinition(int id) =>
        throw new NotImplementedException();
    public void AddFlagDefinition(FlagDefinition definition) =>
        throw new NotImplementedException();
    public void DeleteFlagDefinition(int id) =>
        throw new NotImplementedException();

    public IList<FacetDefinition> GetFacetDefinitions() =>
        throw new NotImplementedException();
    public FacetDefinition? GetFacetDefinition(string id) =>
        throw new NotImplementedException();
    public void AddFacetDefinition(FacetDefinition facet) =>
        throw new NotImplementedException();
    public void DeleteFacetDefinition(string id) =>
        throw new NotImplementedException();

    public IList<string> GetThesaurusIds(ThesaurusFilter? filter = null) =>
        throw new NotImplementedException();
    public DataPage<Thesaurus> GetThesauri(ThesaurusFilter filter) =>
        throw new NotImplementedException();
    public Thesaurus? GetThesaurus(string id) =>
        throw new NotImplementedException();
    public void AddThesaurus(Thesaurus thesaurus) =>
        throw new NotImplementedException();
    public void DeleteThesaurus(string id) =>
        throw new NotImplementedException();
    public IList<string> GetThesaurusAliases(string targetId) =>
        throw new NotImplementedException();

    public DataPage<ItemInfo> GetItems(ItemFilter filter) =>
        throw new NotImplementedException();
    public IItem? GetItem(string id, bool includeParts = true) =>
        throw new NotImplementedException();
    public void AddItem(IItem item, bool history = true) =>
        throw new NotImplementedException();
    public void DeleteItem(string id, string userId, bool history = true) =>
        throw new NotImplementedException();
    public void SetItemFlags(IList<string> ids, int flags) =>
        throw new NotImplementedException();
    public void SetItemGroupId(IList<string> ids, string? groupId) =>
        throw new NotImplementedException();
    public Task<DataPage<string>> GetDistinctGroupIdsAsync(
        PagingOptions options, string? filter = null) =>
        throw new NotImplementedException();
    public Task<int> GetGroupLayersCountAsync(string groupId) =>
        throw new NotImplementedException();
    public DataPage<HistoryItemInfo> GetHistoryItems(
        HistoryItemFilter filter) =>
        throw new NotImplementedException();
    public HistoryItem? GetHistoryItem(string id) =>
        throw new NotImplementedException();
    public void DeleteHistoryItem(string id) =>
        throw new NotImplementedException();

    public DataPage<PartInfo> GetParts(PartFilter filter) =>
        throw new NotImplementedException();
    public IList<LayerPartInfo> GetItemLayerInfo(string itemId,
        bool absent) => throw new NotImplementedException();
    public T? GetPart<T>(string id) where T : class, IPart =>
        throw new NotImplementedException();
    public string? GetPartItemId(string id) =>
        throw new NotImplementedException();
    public string? GetPartContent(string id) =>
        throw new NotImplementedException();
    public void AddPart(IPart part, bool history = true) =>
        throw new NotImplementedException();
    public void AddPartFromContent(string content, bool history = true) =>
        throw new NotImplementedException();
    public void DeletePart(string id, string userId, bool history = true) =>
        throw new NotImplementedException();
    public DataPage<HistoryPartInfo> GetHistoryParts(
        HistoryPartFilter filter) =>
        throw new NotImplementedException();
    public HistoryPart<T>? GetHistoryPart<T>(string id)
        where T : class, IPart => throw new NotImplementedException();
    public void DeleteHistoryPart(string id) =>
        throw new NotImplementedException();
    public string? GetPartCreatorId(string id) =>
        throw new NotImplementedException();
    public int GetLayerPartBreakChance(string id, int toleranceSeconds) =>
        throw new NotImplementedException();
    public IList<LayerHint> GetLayerPartHints(string id) =>
        throw new NotImplementedException();
    public string? ApplyLayerPartPatches(string id, string userId,
        IList<string> patches) => throw new NotImplementedException();
    public void SetPartThesaurusScope(IList<string> ids, string? scope) =>
        throw new NotImplementedException();

    public void AddSetting(string key, string json) =>
        throw new NotImplementedException();
    public string? GetSetting(string key) =>
        throw new NotImplementedException();
    public void DeleteSetting(string key) =>
        throw new NotImplementedException();
    #endregion
}
