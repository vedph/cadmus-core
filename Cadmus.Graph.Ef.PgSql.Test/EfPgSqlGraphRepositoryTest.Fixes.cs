using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Cadmus.Graph.Ef.PgSql.Test;

// wrappers for the regression tests of the fixes in 17.1.0
public sealed partial class EfPgSqlGraphRepositoryTest
{
    private static readonly Lock _schemaLock = new();
    private static bool _schemaUpdated;

    // the test database is reused across runs, and might have been created
    // by an older version: upgrade it once per run
    protected override void OnExistingDatabase()
    {
        lock (_schemaLock)
        {
            if (_schemaUpdated) return;
            PgSqlGraphSchema.ApplyAsync(ConnectionString).GetAwaiter()
                .GetResult();
            _schemaUpdated = true;
        }
    }

#pragma warning disable S2699 // Tests should include assertions
    [Fact]
    public void BuildUid_SameSidUnique_SameUid() =>
        DoBuildUid_SameSidUnique_SameUid();

    [Fact]
    public void BuildUid_OtherSidNotUnique_Shared() =>
        DoBuildUid_OtherSidNotUnique_Shared();

    [Fact]
    public Task BuildUid_Concurrent_AllUnique() =>
        DoBuildUid_Concurrent_AllUnique();

    [Fact]
    public Task AddUri_Concurrent_SingleEntry() =>
        DoAddUri_Concurrent_SingleEntry();

    [Fact]
    public void AddNamespace_Existing_Updated() =>
        DoAddNamespace_Existing_Updated();

    [Fact]
    public void GetNodes_LabelWithWildcards_Escaped() =>
        DoGetNodes_LabelWithWildcards_Escaped();

    [Fact]
    public void ImportNodes_Existing_UpdatedNoError() =>
        DoImportNodes_Existing_UpdatedNoError();

    [Fact]
    public void UpdateGraph_TriplesOnly_Saved() =>
        DoUpdateGraph_TriplesOnly_Saved();

    [Fact]
    public void UpdateGraph_EmptySet_RemovesOld() =>
        DoUpdateGraph_EmptySet_RemovesOld();

    [Fact]
    public void UpdateGraph_SameSet_NoChanges() =>
        DoUpdateGraph_SameSet_NoChanges();

    [Fact]
    public void UpdateGraph_TagChanged_UpdatedInPlace() =>
        DoUpdateGraph_TagChanged_UpdatedInPlace();

    [Fact]
    public void UpdateGraph_LiteralLanguageChanged_Replaced() =>
        DoUpdateGraph_LiteralLanguageChanged_Replaced();

    [Fact]
    public void UpdateGraph_LongLiteral_Saved() =>
        DoUpdateGraph_LongLiteral_Saved();

    [Fact]
    public void UpdateGraph_LongLabel_Truncated() =>
        DoUpdateGraph_LongLabel_Truncated();

    [Fact]
    public void DeleteGraphSet_SharedNodes_Kept() =>
        DoDeleteGraphSet_SharedNodes_Kept();

    [Fact]
    public void UpdateGraph_ImplicitNode_AdoptedByExplicit() =>
        DoUpdateGraph_ImplicitNode_AdoptedByExplicit();

    [Fact]
    public void UpdateGraph_NodeUsedByUser_Kept() =>
        DoUpdateGraph_NodeUsedByUser_Kept();

    [Fact]
    public void UpdateGraph_Classes_Updated() =>
        DoUpdateGraph_Classes_Updated();

    [Fact]
    public void UpdateGraph_ClassCycle_NoEndlessLoop() =>
        DoUpdateGraph_ClassCycle_NoEndlessLoop();

    [Fact]
    public void DeleteTriple_Type_ClassesUpdated() =>
        DoDeleteTriple_Type_ClassesUpdated();

    [Fact]
    public void AddTriple_Type_ClassesUpdated() =>
        DoAddTriple_Type_ClassesUpdated();

    [Fact]
    public Task UpdateNodeClasses_All_Ok() => DoUpdateNodeClasses_All_Ok();

    [Fact]
    public Task UpdateGraph_ConcurrentSources_NoErrors() =>
        DoUpdateGraph_ConcurrentSources_NoErrors();

    [Fact]
    public Task UpdateGraph_ConcurrentSameSource_NoDuplicates() =>
        DoUpdateGraph_ConcurrentSameSource_NoDuplicates();

    [Fact]
    public void FindMappings_ChildWithSameSourceType_NotRoot() =>
        DoFindMappings_ChildWithSameSourceType_NotRoot();

    [Fact]
    public void FindMappings_Filters_Ok() => DoFindMappings_Filters_Ok();

    [Fact]
    public void AddMapping_UnnamedChildren_NotMerged() =>
        DoAddMapping_UnnamedChildren_NotMerged();

    [Fact]
    public void AddMapping_Replaced_NoOrphans() =>
        DoAddMapping_Replaced_NoOrphans();

    [Fact]
    public void DeleteMapping_SharedChild_Kept() =>
        DoDeleteMapping_SharedChild_Kept();

    [Fact]
    public void Export_ManyMappings_AllOnce() => DoExport_ManyMappings_AllOnce();
    [Fact]
    public void GetGraphSet_Ok() => DoGetGraphSet_Ok();

    [Fact]
    public void UpdateGraph_Invalid_Throws() => DoUpdateGraph_Invalid_Throws();

    [Fact]
    public void UpdateGraph_TriplesWithIds_ImplicitNodesFromLookup() => DoUpdateGraph_TriplesWithIds_ImplicitNodesFromLookup();

    [Fact]
    public void UpdateGraph_UserTriplesInSet_Ensured() => DoUpdateGraph_UserTriplesInSet_Ensured();

    [Fact]
    public void GetMappings_Filters_Ok() => DoGetMappings_Filters_Ok();

    [Fact]
    public void AddMapping_SharedChildUpdated_Ok() => DoAddMapping_SharedChildUpdated_Ok();

    [Fact]
    public void AddMapping_NotRoot_Throws() => DoAddMapping_NotRoot_Throws();

    [Fact]
    public void ImportExport_RoundTrip_Ok() => DoImportExport_RoundTrip_Ok();

    [Fact]
    public void GetNamespaces_NoMatch_Empty() => DoGetNamespaces_NoMatch_Empty();

    [Fact]
    public void AddNode_NoUpdate_Unchanged() => DoAddNode_NoUpdate_Unchanged();

    [Fact]
    public void DeleteNode_Class_InstancesUpdated() => DoDeleteNode_Class_InstancesUpdated();

    [Fact]
    public void Update_SameUniqueUidAndSidTwice_DistinctAndStable() => DoUpdate_SameUniqueUidAndSidTwice_DistinctAndStable();

#pragma warning restore S2699 // Tests should include assertions

    [Fact]
    public void FindMappings_Cache_InvalidatedByChanges()
    {
        Reset();
        EfPgSqlGraphRepository repository = (EfPgSqlGraphRepository)
            GetRepository();
        using MemoryCache cache = new(new MemoryCacheOptions());
        repository.Cache = cache;
        RunNodeMappingFilter filter = new() { SourceType = Node.SOURCE_ITEM };
        repository.AddMapping(new GraphNodeMapping
        {
            Name = "m1",
            SourceType = Node.SOURCE_ITEM,
            Source = "."
        });
        Assert.Single(repository.FindMappings(filter));
        Assert.Equal(1, cache.Count);

        // changes via another repository in the same process
        GetRepository().AddMapping(new GraphNodeMapping
        {
            Name = "m2",
            SourceType = Node.SOURCE_ITEM,
            Source = "."
        });

        Assert.Equal(2, repository.FindMappings(filter).Count);
    }
}
