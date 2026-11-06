using Cadmus.Export.Mapping;
using Fusi.Tools.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Graph.Ef.Test;

// regression tests for the graph update, delete, UID and mappings fixes
public abstract partial class EfGraphRepositoryTest
{
    protected const string SRC_A = "aaaaaaaa-0000-4000-8000-000000000001";
    protected const string SRC_B = "bbbbbbbb-0000-4000-8000-000000000002";

    private static UriTriple T(string s, string p, string? o, string sid,
        string? literal = null, string? tag = null) => new()
        {
            SubjectUri = s,
            PredicateUri = p,
            ObjectUri = o,
            ObjectLiteral = literal,
            ObjectLiteralIx = literal?.ToLowerInvariant(),
            Sid = sid,
            Tag = tag
        };

    private static UriNode N(string uri, string sid, string? label = null,
        int sourceType = Node.SOURCE_PART) => new()
        {
            Uri = uri,
            Sid = sid,
            Label = label ?? uri,
            SourceType = sourceType
        };

    private static GraphSet Set(string source, IEnumerable<UriNode>? nodes,
        params UriTriple[] triples)
    {
        GraphSet set = new([.. nodes ?? []], [.. triples]);
        set.SourceIds.Add(source);
        return set;
    }

    private static List<UriTriple> GetAllTriples(IGraphRepository repository)
        => [.. repository.GetTriples(new TripleFilter { PageSize = 0 }).Items];

    #region UID
    protected void DoBuildUid_SameSidUnique_SameUid()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.BuildUid("x:timespans/ts##", "s1");
        string uid2 = repository.BuildUid("x:timespans/ts##", "s2");

        // saving again the same sources gives back the same UIDs
        string uid2Again = repository.BuildUid("x:timespans/ts##", "s2");
        string uid1Again = repository.BuildUid("x:timespans/ts##", "s1");

        Assert.Equal("x:timespans/ts#2", uid2);
        Assert.Equal(uid2, uid2Again);
        Assert.Equal("x:timespans/ts", uid1Again);
    }

    protected void DoBuildUid_OtherSidNotUnique_Shared()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.BuildUid("x:places/arezzo", "s1");

        Assert.Equal("x:places/arezzo",
            repository.BuildUid("x:places/Arezzo", "s2"));
    }

    protected async Task DoBuildUid_Concurrent_AllUnique()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        string[] uids = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(i => Task.Run(() =>
                repository.BuildUid("x:timespans/ts##", $"sid{i}"))));

        Assert.Equal(16, uids.Distinct().Count());
        Assert.Single(uids, u => u == "x:timespans/ts");
    }
    #endregion

    #region URI and namespaces
    protected async Task DoAddUri_Concurrent_SingleEntry()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        int[] ids = await Task.WhenAll(Enumerable.Range(0, 16)
            .Select(i => Task.Run(() =>
                repository.AddUri(i % 2 == 0 ? "x:same" : "x:SAME"))));

        Assert.Single(ids.Distinct());
        Assert.Equal(ids[0], repository.LookupId("x:same"));
    }

    protected void DoAddNamespace_Existing_Updated()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.AddNamespace("x", "http://x.org/");

        repository.AddNamespace("X", "http://y.org/");

        Assert.Equal("http://y.org/", repository.LookupNamespace("x"));
        Assert.Equal(1, repository.GetNamespaces(
            new NamespaceFilter { Prefix = "x" }).Total);
    }
    #endregion

    #region Nodes
    protected void DoGetNodes_LabelWithWildcards_Escaped()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        foreach (string label in new[] { "a_b", "axb", "100%", "1000" })
        {
            repository.AddNode(new Node
            {
                Id = repository.AddUri("x:" + label.Replace('%', 'p')),
                Label = label
            });
        }

        Assert.Equal(1, repository.GetNodes(new NodeFilter { Label = "a_b" })
            .Total);
        Assert.Equal(1, repository.GetNodes(new NodeFilter { Label = "0%" })
            .Total);
        Assert.Equal(1, repository.GetNodes(new NodeFilter { Label = "X" })
            .Total);
    }

    protected void DoImportNodes_Existing_UpdatedNoError()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.ImportNodes([new UriNode { Uri = "x:a", Label = "A" }]);

        repository.ImportNodes(
        [
            new UriNode { Uri = "x:a", Label = "A2" },
            new UriNode { Uri = "x:b", Label = "B" },
            // duplicate in the same batch
            new UriNode { Uri = "x:B", Label = "B" }
        ]);

        Assert.Equal("A2", repository.GetNodeByUri("x:a")!.Label);
        Assert.NotNull(repository.GetNodeByUri("x:b"));
    }
    #endregion

    #region Update graph
    protected void DoUpdateGraph_TriplesOnly_Saved()
    {
        // a set with triples but no nodes for the source (e.g. a mapping
        // emitting only triples about the item's node) was ignored
        Reset();
        IGraphRepository repository = GetRepository();

        repository.UpdateGraph(Set(SRC_A, null,
            T("x:petrarch", "rdfs:label", null, SRC_A + "/label",
                "Petrarch")));

        UriTriple triple = Assert.Single(GetAllTriples(repository));
        Assert.Equal("Petrarch", triple.ObjectLiteral);
        Assert.NotNull(repository.GetNodeByUri("x:petrarch"));
        Assert.Equal(Node.TAG_PROPERTY,
            repository.GetNodeByUri("rdfs:label")!.Tag);
    }

    protected void DoUpdateGraph_EmptySet_RemovesOld()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, [N("x:event", SRC_A + "/e")],
            T("x:event", "rdf:type", "x:birth", SRC_A + "/e")));
        Assert.Single(GetAllTriples(repository));

        // the user removed the event: nothing gets generated
        repository.UpdateGraph(Set(SRC_A, null));

        Assert.Empty(GetAllTriples(repository));
        Assert.Null(repository.GetNodeByUri("x:event"));
        // nodes of the source no longer used were removed
        Assert.Equal(0, repository.GetNodes(new NodeFilter
        {
            Sid = SRC_A,
            IsSidPrefix = true
        }).Total);
    }

    protected void DoUpdateGraph_SameSet_NoChanges()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphSet Build() => Set(SRC_A, [N("x:event", SRC_A + "/e", "Event")],
            T("x:event", "rdf:type", "x:birth", SRC_A + "/e"),
            T("x:event", "rdfs:label", null, SRC_A + "/e", "birth", "t"));
        repository.UpdateGraph(Build());
        List<UriTriple> before = GetAllTriples(repository);
        int nodes = repository.GetNodes(new NodeFilter()).Total;

        GraphSet set = Build();
        repository.UpdateGraph(set);

        List<UriTriple> after = GetAllTriples(repository);
        Assert.Equal(before.Select(t => t.Id).Order(),
            after.Select(t => t.Id).Order());
        Assert.Equal(nodes, repository.GetNodes(new NodeFilter()).Total);
        // the IDs of the received triples are set
        Assert.All(set.Triples, t => Assert.Contains(t.Id,
            after.Select(a => a.Id)));
    }

    protected void DoUpdateGraph_TagChanged_UpdatedInPlace()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, null,
            T("x:a", "x:p", "x:b", SRC_A, tag: "t1")));
        int id = Assert.Single(GetAllTriples(repository)).Id;

        repository.UpdateGraph(Set(SRC_A, null,
            T("x:a", "x:p", "x:b", SRC_A, tag: "t2")));

        UriTriple triple = Assert.Single(GetAllTriples(repository));
        Assert.Equal(id, triple.Id);
        Assert.Equal("t2", triple.Tag);
    }

    protected void DoUpdateGraph_LiteralLanguageChanged_Replaced()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        UriTriple t1 = T("x:a", "rdfs:label", null, SRC_A, "color");
        t1.LiteralLanguage = "en";
        repository.UpdateGraph(Set(SRC_A, null, t1));

        UriTriple t2 = T("x:a", "rdfs:label", null, SRC_A, "color");
        t2.LiteralLanguage = "en-US";
        repository.UpdateGraph(Set(SRC_A, null, t2));

        UriTriple triple = Assert.Single(GetAllTriples(repository));
        Assert.Equal("en-US", triple.LiteralLanguage);
    }

    protected void DoUpdateGraph_LongLiteral_Saved()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        // not compressible, longer than the maximum btree entry
        string literal = string.Concat(Enumerable.Range(0, 300)
            .Select(_ => Guid.NewGuid().ToString("N")));

        repository.UpdateGraph(Set(SRC_A, null,
            T("x:a", "rdfs:comment", null, SRC_A, literal)));

        Assert.Equal(literal,
            Assert.Single(GetAllTriples(repository)).ObjectLiteral);
    }

    protected void DoUpdateGraph_LongLabel_Truncated()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        repository.UpdateGraph(Set(SRC_A,
            [N("x:a", SRC_A, new string('x', 600))]));

        Assert.Equal(500, repository.GetNodeByUri("x:a")!.Label!.Length);
    }

    protected void DoDeleteGraphSet_SharedNodes_Kept()
    {
        // deleting a source used to delete all its nodes, including those
        // (like predicates) used by other sources, whose triples were
        // deleted in cascade
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, null,
            T("x:a1", "x:pred", "x:o", SRC_A + "/1")));
        repository.UpdateGraph(Set(SRC_B, null,
            T("x:b1", "x:pred", "x:o", SRC_B + "/1")));

        repository.DeleteGraphSet(SRC_A);

        UriTriple triple = Assert.Single(GetAllTriples(repository));
        Assert.Equal("x:b1", triple.SubjectUri);
        Assert.Null(repository.GetNodeByUri("x:a1"));
        // shared nodes are handed over to the other source
        UriNode o = repository.GetNodeByUri("x:o")!;
        Assert.Equal(SRC_B + "/1", o.Sid);
        Assert.Equal(Node.SOURCE_IMPLICIT, o.SourceType);
        Assert.Equal(SRC_B + "/1", repository.GetNodeByUri("x:pred")!.Sid);

        // deleting the other source removes its nodes, except properties
        repository.DeleteGraphSet(SRC_B);
        Assert.Empty(GetAllTriples(repository));
        Assert.Null(repository.GetNodeByUri("x:o"));
        Assert.Null(repository.GetNodeByUri("x:b1"));
        Assert.NotNull(repository.GetNodeByUri("x:pred"));
    }

    protected void DoUpdateGraph_ImplicitNode_AdoptedByExplicit()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        // B refers to the work before A defines it
        repository.UpdateGraph(Set(SRC_B, null,
            T("x:event", "x:refers_to", "x:work", SRC_B + "/e")));
        Assert.Equal(Node.SOURCE_IMPLICIT,
            repository.GetNodeByUri("x:work")!.SourceType);

        repository.UpdateGraph(Set(SRC_A, [N("x:work", SRC_A + "/w", "Work")]));

        UriNode work = repository.GetNodeByUri("x:work")!;
        Assert.Equal(SRC_A + "/w", work.Sid);
        Assert.Equal(Node.SOURCE_PART, work.SourceType);
        Assert.Equal("Work", work.Label);

        // deleting B no longer deletes the work
        repository.DeleteGraphSet(SRC_B);
        Assert.NotNull(repository.GetNodeByUri("x:work"));
        Assert.Null(repository.GetNodeByUri("x:event"));
    }

    protected void DoUpdateGraph_NodeUsedByUser_Kept()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, [N("x:event", SRC_A + "/e")]));
        // a user links the event
        repository.ImportNodes(
        [
            new UriNode { Uri = "x:note", Label = "note" },
            new UriNode { Uri = "rdfs:seeAlso", Tag = Node.TAG_PROPERTY },
        ]);
        repository.AddTriple(new Triple
        {
            SubjectId = repository.LookupId("x:note"),
            PredicateId = repository.LookupId("rdfs:seeAlso"),
            ObjectId = repository.GetNodeByUri("x:event")!.Id
        });

        // the event is no longer generated
        repository.UpdateGraph(Set(SRC_A, null));

        UriNode? node = repository.GetNodeByUri("x:event");
        Assert.NotNull(node);
        Assert.Null(node.Sid);
        Assert.Single(GetAllTriples(repository));
    }

    protected void DoUpdateGraph_Classes_Updated()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, [N("x:john", SRC_A + "/j")],
            T("x:john", "rdf:type", "x:artist", SRC_A + "/j"),
            T("x:artist", "rdfs:subClassOf", "x:person", SRC_A + "/j")));
        int artist = repository.LookupId("x:artist");
        int person = repository.LookupId("x:person");

        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { ClassIds = [person] }).Total);
        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { ClassIds = [artist] }).Total);

        // remove the superclass
        repository.UpdateGraph(Set(SRC_A, [N("x:john", SRC_A + "/j")],
            T("x:john", "rdf:type", "x:artist", SRC_A + "/j")));

        Assert.Equal(0, repository.GetNodes(new NodeFilter
        { ClassIds = [person] }).Total);
        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { ClassIds = [artist] }).Total);
    }

    protected void DoUpdateGraph_ClassCycle_NoEndlessLoop()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        repository.UpdateGraph(Set(SRC_A, null,
            T("x:c1", "rdfs:subClassOf", "x:c2", SRC_A),
            T("x:c2", "rdfs:subClassOf", "x:c1", SRC_A),
            T("x:i", "rdf:type", "x:c1", SRC_A)));

        int c2 = repository.LookupId("x:c2");
        UriNode node = Assert.Single(repository.GetNodes(new NodeFilter
        { ClassIds = [c2] }).Items);
        Assert.Equal("x:i", node.Uri);
    }

    protected void DoDeleteTriple_Type_ClassesUpdated()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, null,
            T("x:i", "rdf:type", "x:c", SRC_A)));
        int c = repository.LookupId("x:c");
        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { ClassIds = [c] }).Total);

        repository.DeleteTriple(Assert.Single(GetAllTriples(repository)).Id);

        Assert.Equal(0, repository.GetNodes(new NodeFilter
        { ClassIds = [c] }).Total);
    }

    protected void DoAddTriple_Type_ClassesUpdated()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.ImportNodes(
        [
            new UriNode { Uri = "x:i", Label = "i" },
            new UriNode { Uri = "x:c", Label = "c", IsClass = true },
        ]);
        int c = repository.LookupId("x:c");

        repository.AddTriple(new Triple
        {
            SubjectId = repository.LookupId("x:i"),
            PredicateId = repository.AddUri("rdf:type"),
            ObjectId = c
        });

        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { ClassIds = [c] }).Total);
    }

    protected async Task DoUpdateNodeClasses_All_Ok()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, null,
            T("x:i1", "rdf:type", "x:c", SRC_A),
            T("x:i2", "rdf:type", "x:c", SRC_A)));

        await repository.UpdateNodeClassesAsync(default);

        Assert.Equal(2, repository.GetNodes(new NodeFilter
        { ClassIds = [repository.LookupId("x:c")] }).Total);
    }

    protected async Task DoUpdateGraph_ConcurrentSources_NoErrors()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        string[] sources = [.. Enumerable.Range(1, 8).Select(i =>
            $"{i:x8}-0000-4000-8000-000000000000")];

        // all the sources create the same new nodes at the same time
        await Task.WhenAll(sources.Select(s => Task.Run(() =>
            repository.UpdateGraphAsync(Set(s, null,
                T($"x:s/{s}", "x:shared_pred", "x:shared_obj", s + "/1"),
                T("x:shared_obj", "rdf:type", "x:shared_class", s + "/1"))))));

        // and update them again
        await Task.WhenAll(sources.Select(s => Task.Run(() =>
            repository.UpdateGraphAsync(Set(s, null,
                T($"x:s/{s}", "x:shared_pred", "x:shared_obj", s + "/1"))))));

        Assert.Equal(8, GetAllTriples(repository).Count);
        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { Uid = "x:shared_obj" }).Total);
    }

    protected async Task DoUpdateGraph_ConcurrentSameSource_NoDuplicates()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
            repository.UpdateGraphAsync(Set(SRC_A, null,
                T("x:a", "x:p", "x:b", SRC_A + "/1"),
                T("x:a", "x:q", null, SRC_A + "/1", "lit"))))));

        Assert.Equal(2, GetAllTriples(repository).Count);
    }
    #endregion

    #region Mappings
    private static GraphNodeMapping GetMapping(string name, string source,
        int sourceType = Node.SOURCE_ITEM) => new()
        {
            Name = name,
            SourceType = sourceType,
            Source = source,
            Sid = "{$item-id}",
            Output = new NodeMappingOutput
            {
                Nodes = new Dictionary<string, MappedNode>
                {
                    ["n"] = new MappedNode { Uid = "x:" + name }
                }
            }
        };

    protected void DoFindMappings_ChildWithSameSourceType_NotRoot()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping root = GetMapping("root", ".");
        root.Children.Add(GetMapping("child", "."));
        repository.AddMapping(root);

        IList<GraphNodeMapping> mappings = repository.FindMappings(
            new RunNodeMappingFilter { SourceType = Node.SOURCE_ITEM });

        GraphNodeMapping found = Assert.Single(mappings);
        Assert.Equal("root", found.Name);
        Assert.Single(found.Children);
    }

    protected void DoFindMappings_Filters_Ok()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping group = GetMapping("group", ".");
        group.GroupFilter = "^alpha";
        repository.AddMapping(group);
        GraphNodeMapping flags = GetMapping("flags", ".");
        flags.FlagsFilter = 1;
        repository.AddMapping(flags);
        GraphNodeMapping title = GetMapping("title", ".");
        title.TitleFilter = "\\[#";
        repository.AddMapping(title);
        GraphNodeMapping bad = GetMapping("bad", ".");
        bad.GroupFilter = "([";
        repository.AddMapping(bad);
        GraphNodeMapping role = GetMapping("role", ".", Node.SOURCE_PART);
        role.PartRoleFilter = "r1";
        repository.AddMapping(role);

        string[] Find(RunNodeMappingFilter f) =>
            [.. repository.FindMappings(f).Select(m => m.Name!).Order()];

        Assert.Equal(["group"], Find(new RunNodeMappingFilter
        {
            SourceType = Node.SOURCE_ITEM,
            Group = "alpha-1",
            Flags = 2,
            Title = "x"
        }));
        Assert.Empty(Find(new RunNodeMappingFilter
        {
            SourceType = Node.SOURCE_ITEM,
            Group = "beta",
            Flags = 0
        }));
        Assert.Equal(["flags", "title"], Find(new RunNodeMappingFilter
        {
            SourceType = Node.SOURCE_ITEM,
            Flags = 3,
            Title = "Petrarch [#x:petrarch]"
        }));
        Assert.Empty(Find(new RunNodeMappingFilter
        {
            SourceType = Node.SOURCE_PART,
            PartRole = null
        }));
        Assert.Equal(["role"], Find(new RunNodeMappingFilter
        {
            SourceType = Node.SOURCE_PART,
            PartRole = "R1"
        }));
    }

    protected void DoAddMapping_UnnamedChildren_NotMerged()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping root = GetMapping("root", ".");
        GraphNodeMapping c1 = GetMapping("", "a");
        c1.Name = null;
        GraphNodeMapping c2 = GetMapping("", "b");
        c2.Name = null;
        root.Children.Add(c1);
        root.Children.Add(c2);

        int id = repository.AddMapping(root);

        GraphNodeMapping? mapping = repository.GetMapping(id);
        Assert.NotNull(mapping);
        Assert.Equal(["a", "b"],
            mapping.Children.Select(c => c.Source).Order());
    }

    protected void DoAddMapping_Replaced_NoOrphans()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping root = GetMapping("root", ".");
        root.Children.Add(GetMapping("c1", "a"));
        repository.AddMapping(root);

        root = GetMapping("root", ".");
        root.Children.Add(GetMapping("c2", "b"));
        int id = repository.AddMapping(root);

        // the old child would appear as a root if orphaned
        DataPage<GraphNodeMapping> page = repository.GetMappings(
            new NodeMappingFilter(), false);
        Assert.Equal(1, page.Total);
        GraphNodeMapping mapping = repository.GetMapping(id)!;
        Assert.Equal("c2", Assert.Single(mapping.Children).Name);
    }

    protected void DoDeleteMapping_SharedChild_Kept()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping r1 = GetMapping("r1", ".");
        r1.Children.Add(GetMapping("shared", "a"));
        int id1 = repository.AddMapping(r1);
        GraphNodeMapping r2 = GetMapping("r2", ".");
        r2.Children.Add(GetMapping("shared", "a"));
        int id2 = repository.AddMapping(r2);

        repository.DeleteMapping(id1);

        Assert.Null(repository.GetMapping(id1));
        Assert.Equal("shared",
            Assert.Single(repository.GetMapping(id2)!.Children).Name);

        repository.DeleteMapping(id2);
        Assert.Equal(0, repository.GetMappings(new NodeMappingFilter(),
            false).Total);
    }

    protected void DoExport_ManyMappings_AllOnce()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        for (int i = 0; i < 30; i++)
            repository.AddMapping(GetMapping($"m{i:00}", "."));

        string json = repository.Export();

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        options.Converters.Add(new NodeMappingOutputJsonConverter());
        options.Converters.Add(new NodeMappingJsonConverter<GraphNodeMapping>());
        NodeMappingDocument doc = JsonSerializer.Deserialize<NodeMappingDocument>(
            json, options)!;
        Assert.Equal(30, doc.DocumentMappings.Count);
        Assert.Equal(30, doc.DocumentMappings.Select(m => m.Name).Distinct()
            .Count());
    }
    #endregion

    #region Coverage
    protected void DoGetGraphSet_Ok()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, [N("x:a", SRC_A + "/1")],
            T("x:a", "x:p", "x:b", SRC_A + "/1")));
        repository.UpdateGraph(Set(SRC_B, null,
            T("x:c", "x:p", "x:d", SRC_B + "/1")));

        GraphSet set = repository.GetGraphSet(SRC_A.ToUpperInvariant());

        // a, its implicit object b and predicate p have SID A
        Assert.Equal(["x:a", "x:b", "x:p"],
            set.Nodes.Select(n => n.Uri!).Order());
        UriTriple triple = Assert.Single(set.Triples);
        Assert.Equal("x:a", triple.SubjectUri);
        Assert.Equal("x:p", triple.PredicateUri);
        Assert.Equal("x:b", triple.ObjectUri);
    }

    protected void DoUpdateGraph_Invalid_Throws()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_A, [new UriNode { Sid = SRC_A }])));
        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_A, null, T(null!, "x:p", "x:o", SRC_A))));
        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_A, null, T("x:s", null!, "x:o", SRC_A))));
        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_A, null, T("x:s", "x:p", null, SRC_A))));
        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_A, [N("x:" + new string('a', 500), SRC_A)])));
        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_A, [N(" ", SRC_A)])));
        // nothing was saved
        Assert.Empty(GetAllTriples(repository));
    }

    protected void DoUpdateGraph_TriplesWithIds_ImplicitNodesFromLookup()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        int s = repository.AddUri("x:s"), p = repository.AddUri("x:p"),
            o = repository.AddUri("x:o");

        repository.UpdateGraph(Set(SRC_A, null, new UriTriple
        {
            SubjectId = s,
            PredicateId = p,
            ObjectId = o,
            Sid = SRC_A
        }));

        Assert.Single(GetAllTriples(repository));
        UriNode predicate = repository.GetNode(p)!;
        Assert.Equal("x:p", predicate.Label);
        Assert.Equal(Node.TAG_PROPERTY, predicate.Tag);
        Assert.Equal(Node.SOURCE_IMPLICIT, repository.GetNode(o)!.SourceType);

        // an ID without URI cannot be resolved
        Assert.Throws<CadmusGraphException>(() => repository.UpdateGraph(
            Set(SRC_B, null, new UriTriple
            {
                SubjectId = 999999,
                PredicateId = p,
                ObjectLiteral = "x",
                Sid = SRC_B
            })));
    }

    protected void DoUpdateGraph_UserTriplesInSet_Ensured()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphSet set = new();
        set.Triples.Add(T("x:s", "x:p", "x:o", null!));

        repository.UpdateGraph(set);
        int id = set.Triples[0].Id;
        repository.UpdateGraph(set);

        UriTriple triple = Assert.Single(GetAllTriples(repository));
        Assert.Equal(id, triple.Id);
        Assert.Null(triple.Sid);
    }

    protected void DoGetMappings_Filters_Ok()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping a = GetMapping("alpha", ".");
        a.FacetFilter = "person";
        a.GroupFilter = "^grp";
        a.FlagsFilter = 3;
        a.TitleFilter = "title";
        a.Children.Add(GetMapping("alpha-child", "x"));
        repository.AddMapping(a);
        GraphNodeMapping b = GetMapping("beta", ".", Node.SOURCE_PART);
        b.PartTypeFilter = "it.vedph.events";
        b.PartRoleFilter = "r";
        repository.AddMapping(b);

        string[] Get(NodeMappingFilter f) => [.. repository.GetMappings(f, false)
            .Items.Select(m => m.Name!)];

        Assert.Equal(["alpha", "beta"], Get(new NodeMappingFilter()));
        Assert.Equal(["alpha"], Get(new NodeMappingFilter { Name = "ALP" }));
        Assert.Equal(["alpha"], Get(new NodeMappingFilter
        { SourceType = Node.SOURCE_ITEM }));
        Assert.Equal(["alpha"], Get(new NodeMappingFilter { Facet = "Person" }));
        Assert.Equal(["alpha"], Get(new NodeMappingFilter { Group = "grp" }));
        Assert.Equal(["alpha"], Get(new NodeMappingFilter { Flags = 1 }));
        Assert.Empty(Get(new NodeMappingFilter { Flags = 4 }));
        Assert.Equal(["alpha"], Get(new NodeMappingFilter { Title = "tit" }));
        Assert.Equal(["beta"], Get(new NodeMappingFilter
        { PartType = "it.vedph.events" }));
        Assert.Equal(["beta"], Get(new NodeMappingFilter { PartRole = "R" }));
        Assert.Empty(Get(new NodeMappingFilter { Name = "none" }));
        Assert.Equal(["alpha-child"], Get(new NodeMappingFilter
        { ParentId = a.Id }));

        // paging
        DataPage<GraphNodeMapping> page = repository.GetMappings(
            new NodeMappingFilter { PageNumber = 2, PageSize = 1 }, true);
        Assert.Equal(2, page.Total);
        Assert.Equal("beta", Assert.Single(page.Items).Name);
    }

    protected void DoAddMapping_SharedChildUpdated_Ok()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping child1 = GetMapping("child", "a");
        child1.Children.Add(GetMapping("grandchild", "g"));
        GraphNodeMapping r1 = GetMapping("r1", ".");
        r1.Children.Add(child1);
        int id1 = repository.AddMapping(r1);

        // the same named child, redefined without its grandchild
        GraphNodeMapping child2 = GetMapping("child", "b");
        child2.Output!.Metadata["k"] = "v";
        GraphNodeMapping r2 = GetMapping("r2", ".");
        r2.Children.Add(child2);
        repository.AddMapping(r2);

        // the child is shared, so it's updated for both roots
        GraphNodeMapping child = (GraphNodeMapping)Assert.Single(
            repository.GetMapping(id1)!.Children);
        Assert.Equal("b", child.Source);
        Assert.Equal("v", child.Output!.Metadata["k"]);
        Assert.False(child.HasChildren);
        // the grandchild was removed, rather than left as an orphan root
        Assert.Equal(2, repository.GetMappings(new NodeMappingFilter(),
            false).Total);
    }

    protected void DoAddMapping_NotRoot_Throws()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping mapping = GetMapping("m", ".");
        mapping.ParentId = 1;

        Assert.Throws<InvalidOperationException>(
            () => repository.AddMapping(mapping));
        Assert.Throws<InvalidDataException>(() => repository.Import("null"));
        Assert.Null(repository.GetMapping(12345));
    }

    protected void DoImportExport_RoundTrip_Ok()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        GraphNodeMapping root = GetMapping("root", ".");
        root.Children.Add(GetMapping("child", "c"));
        repository.AddMapping(root);
        string json = repository.Export();

        Reset();
        repository = GetRepository();
        Assert.Equal(1, repository.Import(json));

        GraphNodeMapping imported = Assert.Single(repository.GetMappings(
            new NodeMappingFilter(), true).Items);
        Assert.Equal("root", imported.Name);
        Assert.Equal("child", Assert.Single(imported.Children).Name);
    }

    protected void DoGetNamespaces_NoMatch_Empty()
    {
        Reset();
        IGraphRepository repository = GetRepository();

        Assert.Equal(0, repository.GetNamespaces(new NamespaceFilter
        { Uri = "nothing" }).Total);
    }

    protected void DoAddNode_NoUpdate_Unchanged()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        Node node = new() { Id = repository.AddUri("x:a"), Label = "A" };
        repository.AddNode(node);

        repository.AddNode(new Node { Id = node.Id, Label = "B" }, true);
        Assert.Equal("A", repository.GetNode(node.Id)!.Label);

        repository.AddNode(new Node { Id = node.Id, Label = "B" });
        Assert.Equal("B", repository.GetNode(node.Id)!.Label);
    }

    protected void DoUpdate_SameUniqueUidAndSidTwice_DistinctAndStable()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.AddMapping(new GraphNodeMapping
        {
            Name = "timespans",
            SourceType = Node.SOURCE_PART,
            Source = "events[0].chronotopes",
            // the same SID for all the chronotopes of the event
            Sid = "{$part-id}/chronotopes",
            Output = new NodeMappingOutput
            {
                Nodes = new Dictionary<string, MappedNode>
                {
                    ["ts"] = new MappedNode { Uid = "x:timespans/ts##" }
                },
                Triples =
                [
                    new MappedTriple { S = "{?ts}", P = "a", O = "x:timespan" }
                ]
            }
        });
        Core.Item item = new()
        {
            Title = "Petrarch",
            FacetId = "person",
            UserId = "zeus",
            CreatorId = "zeus"
        };
        General.Parts.HistoricalEventsPart part = new()
        {
            ItemId = item.Id,
            UserId = "zeus",
            CreatorId = "zeus"
        };
        part.Events.Add(new General.Parts.HistoricalEvent
        {
            Eid = "birth",
            Type = "birth",
            Chronotopes =
            [
                new Refs.Bricks.AssertedChronotope
                {
                    Place = new Refs.Bricks.AssertedPlace { Value = "a" }
                },
                new Refs.Bricks.AssertedChronotope
                {
                    Place = new Refs.Bricks.AssertedPlace { Value = "b" }
                }
            ]
        });
        GraphUpdater updater = new(repository);

        updater.Update(item, part);
        List<UriTriple> first = GetAllTriples(repository);
        updater.Update(item, part);
        List<UriTriple> second = GetAllTriples(repository);

        Assert.Equal(2, first.Count);
        Assert.Equal(2, first.Select(t => t.SubjectUri).Distinct().Count());
        Assert.Equal(first.Select(t => t.Id).Order(),
            second.Select(t => t.Id).Order());
        Assert.Equal(first.Select(t => t.SubjectUri).Order(),
            second.Select(t => t.SubjectUri).Order());
    }

    protected void DoDeleteNode_Class_InstancesUpdated()
    {
        Reset();
        IGraphRepository repository = GetRepository();
        repository.UpdateGraph(Set(SRC_A, null,
            T("x:i", "rdf:type", "x:c", SRC_A),
            T("x:c", "rdfs:subClassOf", "x:d", SRC_A)));
        int d = repository.LookupId("x:d");
        Assert.Equal(1, repository.GetNodes(new NodeFilter
        { ClassIds = [d] }).Total);

        repository.DeleteNode(repository.LookupId("x:c"));

        Assert.Equal(0, repository.GetNodes(new NodeFilter
        { ClassIds = [d] }).Total);
        Assert.Null(repository.GetNodeByUri("x:c"));
    }
    #endregion
}
