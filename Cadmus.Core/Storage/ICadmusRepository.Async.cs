using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Cadmus.Core.Config;
using Cadmus.Core.Layers;
using Fusi.Tools.Data;

namespace Cadmus.Core.Storage;

// Asynchronous members of ICadmusRepository. Each of them has the same
// semantics of its synchronous counterpart, plus cancellation. They are
// default interface members wrapping the synchronous ones, so that existing
// implementations keep working unchanged; storage implementations should
// override them with natively asynchronous code (as MongoCadmusRepository
// does). Code running in a server (like the API) should prefer them, so
// that no thread is blocked while waiting for the database.
public partial interface ICadmusRepository
{
    #region Flags
    /// <summary>
    /// Gets the flag definitions.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Definitions.</returns>
    Task<IList<FlagDefinition>> GetFlagDefinitionsAsync(
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetFlagDefinitions());
    }

    /// <summary>
    /// Gets the specified flag definition.
    /// </summary>
    /// <param name="id">The flag identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Definition or null if not found.</returns>
    Task<FlagDefinition?> GetFlagDefinitionAsync(int id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetFlagDefinition(id));
    }

    /// <summary>
    /// Adds or updates the specified flag definition.
    /// </summary>
    /// <param name="definition">The definition.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddFlagDefinitionAsync(FlagDefinition definition,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddFlagDefinition(definition);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the specified flag definition.
    /// </summary>
    /// <param name="id">The flag identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteFlagDefinitionAsync(int id, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteFlagDefinition(id);
        return Task.CompletedTask;
    }
    #endregion

    #region Facets
    /// <summary>
    /// Gets the item's facets.
    /// </summary>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Facets.</returns>
    Task<IList<FacetDefinition>> GetFacetDefinitionsAsync(
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetFacetDefinitions());
    }

    /// <summary>
    /// Gets the specified item's facet.
    /// </summary>
    /// <param name="id">The facet identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Facet or null if not found.</returns>
    Task<FacetDefinition?> GetFacetDefinitionAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetFacetDefinition(id));
    }

    /// <summary>
    /// Adds or updates the specified facet.
    /// </summary>
    /// <param name="facet">The facet.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddFacetDefinitionAsync(FacetDefinition facet,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddFacetDefinition(facet);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the specified facet.
    /// </summary>
    /// <param name="id">The facet identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteFacetDefinitionAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteFacetDefinition(id);
        return Task.CompletedTask;
    }
    #endregion

    #region Thesauri
    /// <summary>
    /// Gets the IDs of all the thesauri, or of all those matching the
    /// specified filter.
    /// </summary>
    /// <param name="filter">The optional filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>IDs.</returns>
    Task<IList<string>> GetThesaurusIdsAsync(ThesaurusFilter? filter = null,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetThesaurusIds(filter));
    }

    /// <summary>
    /// Gets the specified page of thesauri.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Page.</returns>
    Task<DataPage<Thesaurus>> GetThesauriAsync(ThesaurusFilter filter,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetThesauri(filter));
    }

    /// <summary>
    /// Gets the thesaurus with the specified ID.
    /// </summary>
    /// <param name="id">The thesaurus ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Thesaurus, or null if not found.</returns>
    Task<Thesaurus?> GetThesaurusAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetThesaurus(id));
    }

    /// <summary>
    /// Adds or updates the specified thesaurus.
    /// </summary>
    /// <param name="thesaurus">The thesaurus.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddThesaurusAsync(Thesaurus thesaurus,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddThesaurus(thesaurus);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the specified thesaurus.
    /// </summary>
    /// <param name="id">The thesaurus ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteThesaurusAsync(string id, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteThesaurus(id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets all the thesauri IDs which are aliases directly pointing to the
    /// thesaurus with the specified ID.
    /// </summary>
    /// <param name="targetId">The target thesaurus identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>List of alias IDs in alphabetical order.</returns>
    Task<IList<string>> GetThesaurusAliasesAsync(string targetId,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetThesaurusAliases(targetId));
    }
    #endregion

    #region Items
    /// <summary>
    /// Gets a page of items.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Items page.</returns>
    Task<DataPage<ItemInfo>> GetItemsAsync(ItemFilter filter,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetItems(filter));
    }

    /// <summary>
    /// Gets the specified item.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="includeParts">if set to <c>true</c>, include all the
    /// item's parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Item or null if not found.</returns>
    Task<IItem?> GetItemAsync(string id, bool includeParts = true,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetItem(id, includeParts));
    }

    /// <summary>
    /// Adds or updates the specified item.
    /// </summary>
    /// <param name="item">The item.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddItemAsync(IItem item, bool history = true,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddItem(item, history);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the item.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="userId">The user identifier.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteItemAsync(string id, string userId, bool history = true,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteItem(id, userId, history);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Set the flags of the item(s) with the specified ID(s).
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The item identifier(s).</param>
    /// <param name="flags">The flags value.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task SetItemFlagsAsync(IList<string> ids, int flags,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        SetItemFlags(ids, flags);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Set the group ID of the item(s) with the specified ID(s).
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The items identifiers.</param>
    /// <param name="groupId">The group ID (can be null).</param>
    /// <param name="cancel">The cancellation token.</param>
    Task SetItemGroupIdAsync(IList<string> ids, string? groupId,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        SetItemGroupId(ids, groupId);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a page of history items.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>History items page.</returns>
    Task<DataPage<HistoryItemInfo>> GetHistoryItemsAsync(
        HistoryItemFilter filter, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetHistoryItems(filter));
    }

    /// <summary>
    /// Gets the specified history item.
    /// </summary>
    /// <param name="id">The history item's identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Item or null if not found.</returns>
    Task<HistoryItem?> GetHistoryItemAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetHistoryItem(id));
    }

    /// <summary>
    /// Deletes the specified history item.
    /// </summary>
    /// <param name="id">The history item's identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteHistoryItemAsync(string id, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteHistoryItem(id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Exports the item with the specified ID as a JSON document to the
    /// given writer. See <see cref="ExportItem"/>.
    /// </summary>
    /// <param name="id">The item's identifier.</param>
    /// <param name="writer">The writer to export the item to. The writer
    /// is flushed but not closed.</param>
    /// <param name="includeParts">if set to <c>true</c>, include all the item's
    /// parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>True if the item was found and exported, false if not found.
    /// </returns>
    Task<bool> ExportItemAsync(string id, TextWriter writer,
        bool includeParts = true, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(ExportItem(id, writer, includeParts));
    }

    /// <summary>
    /// Imports a new item with its parts from a JSON document read from the
    /// given reader. See <see cref="ImportItem"/>.
    /// </summary>
    /// <param name="reader">The reader to import the item from.</param>
    /// <param name="userId">The ID of the user performing the import.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The imported item with its parts, having their final IDs.
    /// </returns>
    Task<IItem> ImportItemAsync(TextReader reader, string? userId = null,
        bool history = true, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(ImportItem(reader, userId, history));
    }
    #endregion

    #region Parts
    /// <summary>
    /// Gets the specified page of parts.
    /// </summary>
    /// <param name="filter">The parts filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Parts page.</returns>
    Task<DataPage<PartInfo>> GetPartsAsync(PartFilter filter,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetParts(filter));
    }

    /// <summary>
    /// Gets the parts belonging to the specified item(s), eventually
    /// filtered by their type and/or role.
    /// </summary>
    /// <param name="itemIds">The item ID(s).</param>
    /// <param name="typeId">The optional type identifier.</param>
    /// <param name="roleId">The optional role identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Parts.</returns>
    Task<IList<IPart>> GetItemPartsAsync(string[] itemIds,
        string? typeId = null, string? roleId = null,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetItemParts(itemIds, typeId, roleId));
    }

    /// <summary>
    /// Gets layer parts information about the item with the specified ID.
    /// </summary>
    /// <param name="itemId">The item's identifier.</param>
    /// <param name="absent">if set to <c>true</c>, include also information
    /// about absent parts.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Layers parts information.</returns>
    Task<IList<LayerPartInfo>> GetItemLayerInfoAsync(string itemId,
        bool absent, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetItemLayerInfo(itemId, absent));
    }

    /// <summary>
    /// Gets the specified part.
    /// </summary>
    /// <typeparam name="T">The type of the part to retrieve.</typeparam>
    /// <param name="id">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Part or null if not found.</returns>
    Task<T?> GetPartAsync<T>(string id, CancellationToken cancel = default)
        where T : class, IPart
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetPart<T>(id));
    }

    /// <summary>
    /// Gets the identifier of the item including the part with the
    /// specified part identifier.
    /// </summary>
    /// <param name="id">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The item identifier, or null if part not found.</returns>
    Task<string?> GetPartItemIdAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetPartItemId(id));
    }

    /// <summary>
    /// Gets the code representing the part with the specified ID.
    /// </summary>
    /// <param name="id">The part identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>JSON code or null if not found.</returns>
    Task<string?> GetPartContentAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetPartContent(id));
    }

    /// <summary>
    /// Adds or updates the specified part.
    /// </summary>
    /// <param name="part">The part.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddPartAsync(IPart part, bool history = true,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddPart(part, history);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds or updates the specified part from its encoded content.
    /// </summary>
    /// <param name="content">The content.</param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddPartFromContentAsync(string content, bool history = true,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddPartFromContent(content, history);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Deletes the specified part.
    /// </summary>
    /// <param name="id">The part's identifier.</param>
    /// <param name="userId">The identifier of the user deleting the part.
    /// </param>
    /// <param name="history">if set to <c>true</c>, the history should be
    /// affected.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeletePartAsync(string id, string userId, bool history = true,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeletePart(id, userId, history);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a page of history parts.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>History parts page.</returns>
    Task<DataPage<HistoryPartInfo>> GetHistoryPartsAsync(
        HistoryPartFilter filter, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetHistoryParts(filter));
    }

    /// <summary>
    /// Gets the specified history part.
    /// </summary>
    /// <typeparam name="T">The part type.</typeparam>
    /// <param name="id">The identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Part or null if not found.</returns>
    Task<HistoryPart<T>?> GetHistoryPartAsync<T>(string id,
        CancellationToken cancel = default) where T : class, IPart
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetHistoryPart<T>(id));
    }

    /// <summary>
    /// Deletes the specified history part.
    /// </summary>
    /// <param name="id">The history part's identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteHistoryPartAsync(string id, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteHistoryPart(id);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the ID of the creator of the part with the specified ID.
    /// </summary>
    /// <param name="id">The part ID.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>Creator ID, or null.</returns>
    Task<string?> GetPartCreatorIdAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetPartCreatorId(id));
    }

    /// <summary>
    /// Determines whether the layer part with the specified ID might
    /// potentially have been broken because of changes in its base text.
    /// See <see cref="GetLayerPartBreakChance"/>.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="toleranceSeconds">The count of seconds representing
    /// the tolerance time interval between a base text save time and that
    /// of its layer part.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns><c>0</c> if the layer part is not potentially broken;
    /// <c>1</c> if it's potentially broken; <c>2</c> if it's surely broken.
    /// </returns>
    Task<int> GetLayerPartBreakChanceAsync(string id, int toleranceSeconds,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetLayerPartBreakChance(id, toleranceSeconds));
    }

    /// <summary>
    /// Gets the layer part reconciliation hints.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The hints, one for each fragment in the layer part.</returns>
    Task<IList<LayerHint>> GetLayerPartHintsAsync(string id,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetLayerPartHints(id));
    }

    /// <summary>
    /// Applies the specified patches instructions to the layer part
    /// with ID equal to <paramref name="id"/>.
    /// </summary>
    /// <param name="id">The layer part identifier.</param>
    /// <param name="userId">The user identifier. This will be set as
    /// the author of the changes in the part.</param>
    /// <param name="patches">The patch instructions.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The patched layer part content or null if layer part
    /// not found.</returns>
    Task<string?> ApplyLayerPartPatchesAsync(string id, string userId,
        IList<string> patches, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(ApplyLayerPartPatches(id, userId, patches));
    }

    /// <summary>
    /// Set the <see cref="IPart.ThesaurusScope"/> property of all the
    /// parts with the specified ID.
    /// Note that this operation never affects the item's history.
    /// </summary>
    /// <param name="ids">The part identifier(s).</param>
    /// <param name="scope">The new scope (may be null).</param>
    /// <param name="cancel">The cancellation token.</param>
    Task SetPartThesaurusScopeAsync(IList<string> ids, string? scope,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        SetPartThesaurusScope(ids, scope);
        return Task.CompletedTask;
    }
    #endregion

    #region Settings
    /// <summary>
    /// Adds or updates the settings with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="json">The JSON code representing the settings value.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task AddSettingAsync(string key, string json,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        AddSetting(key, json);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets the setting with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <returns>The JSON code representing the settings value, or null
    /// if not found.</returns>
    Task<string?> GetSettingAsync(string key,
        CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        return Task.FromResult(GetSetting(key));
    }

    /// <summary>
    /// Deletes the setting with the specified key.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="cancel">The cancellation token.</param>
    Task DeleteSettingAsync(string key, CancellationToken cancel = default)
    {
        cancel.ThrowIfCancellationRequested();
        DeleteSetting(key);
        return Task.CompletedTask;
    }
    #endregion
}
