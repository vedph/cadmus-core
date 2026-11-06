using Cadmus.Core;
using Cadmus.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Graph.Test;

/// <summary>
/// Tests for the fixes to the core graph components introduced in 17.1.0.
/// </summary>
public sealed class GraphFixesTest
{
    #region LiteralHelper
    [Theory]
    [InlineData("\"abc\"", "abc", null, null)]
    [InlineData("\"abc\"@en", "abc", null, "en")]
    // BCP47 tags with subtags
    [InlineData("\"abc\"@en-US", "abc", null, "en-US")]
    [InlineData("\"abc\"@zh-Hant-TW", "abc", null, "zh-Hant-TW")]
    [InlineData("\"12\"^^xsd:int", "12", "xsd:int", null)]
    // quotes inside the value are preserved
    [InlineData("\"He said \"hi\"\"", "He said \"hi\"", null, null)]
    [InlineData("\"He said \"hi\"\"@en", "He said \"hi\"", null, "en")]
    // @ and ^^ inside the value are not suffixes
    [InlineData("\"mail me at a@b\"", "mail me at a@b", null, null)]
    [InlineData("\"2^^3\"", "2^^3", null, null)]
    // unquoted literal is left as is
    [InlineData("abc", "abc", null, null)]
    // empty value
    [InlineData("\"\"", "", null, null)]
    // unrecognized suffix: keep the whole literal minus its opening quote
    [InlineData("\"abc\"x", "abc\"x", null, null)]
    public void ParseLiteral_Ok(string literal, string value, string? type,
        string? lang)
    {
        (string v, string? t, string? l) = LiteralHelper.ParseLiteral(literal);

        Assert.Equal(value, v);
        Assert.Equal(type, t);
        Assert.Equal(lang, l);
    }

    [Fact]
    public void AdjustLiteral_LanguageWithSubtag_Ok()
    {
        Triple triple = new() { ObjectLiteral = "\"color\"@en-US" };

        LiteralHelper.AdjustLiteral(triple);

        Assert.Equal("color", triple.ObjectLiteral);
        Assert.Equal("en-US", triple.LiteralLanguage);
        Assert.Null(triple.LiteralType);
        Assert.Equal("color", triple.ObjectLiteralIx);
    }

    [Fact]
    public void AdjustLiteral_MalformedNumber_NoNumber()
    {
        Triple triple = new() { ObjectLiteral = "\"x12\"^^xsd:int" };

        LiteralHelper.AdjustLiteral(triple);

        Assert.Equal("x12", triple.ObjectLiteral);
        Assert.Equal("xsd:int", triple.LiteralType);
        Assert.Null(triple.LiteralNumber);
    }
    #endregion

    #region GraphSet
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("short", "short")]
    [InlineData("dbf5c6aa-7781-45fd-accb-5c5365e2596f",
        "dbf5c6aa-7781-45fd-accb-5c5365e2596f")]
    [InlineData("dbf5c6aa-7781-45fd-accb-5c5365e2596f/alpha/x",
        "dbf5c6aa-7781-45fd-accb-5c5365e2596f")]
    public void GetSourceGuid_Ok(string? sid, string expected)
    {
        Assert.Equal(expected, GraphSet.GetSourceGuid(sid));
    }

    [Fact]
    public void GetNodesByGuid_ShortSid_NoThrow()
    {
        GraphSet set = new();
        set.Nodes.Add(new UriNode { Uri = "x:a", Sid = "short" });
        set.Nodes.Add(new UriNode { Uri = "x:b" });

        IDictionary<string, IList<UriNode>> groups = set.GetNodesByGuid();

        Assert.Equal(2, groups.Count);
        Assert.Single(groups["short"]);
        Assert.Single(groups[""]);
    }

    [Fact]
    public void GetTriplesByGuid_CaseInsensitive()
    {
        const string guid = "dbf5c6aa-7781-45fd-accb-5c5365e2596f";
        GraphSet set = new();
        set.Triples.Add(new UriTriple { Sid = guid + "/a" });
        set.Triples.Add(new UriTriple { Sid = guid.ToUpperInvariant() + "/b" });

        IDictionary<string, IList<UriTriple>> groups = set.GetTriplesByGuid();

        Assert.Single(groups);
        Assert.Equal(2, groups[guid].Count);
    }

    [Fact]
    public void SourceIds_CaseInsensitive()
    {
        GraphSet set = new();
        set.SourceIds.Add("ABC");
        set.SourceIds.Add("abc");

        Assert.Single(set.SourceIds);
        Assert.Contains("Abc", set.SourceIds);
    }
    #endregion

    #region UidResolver and RamUidBuilder
    [Theory]
    [InlineData("x:a", "x:a", false)]
    [InlineData("x:a##", "x:a", true)]
    [InlineData("x:a#", "x:a#", false)]
    public void UidResolver_Parse_Ok(string uid, string unsuffixed, bool unique)
    {
        (string u, bool q) = UidResolver.Parse(uid);
        Assert.Equal(unsuffixed, u);
        Assert.Equal(unique, q);
    }

    [Fact]
    public void UidResolver_NoEntries_NewUnsuffixed()
    {
        string? uid = UidResolver.TryResolve([], "x:a", "s1", true,
            out bool hasSuffix);
        Assert.Null(uid);
        Assert.False(hasSuffix);
    }

    [Fact]
    public void UidResolver_SameSid_Reused()
    {
        UidEntry[] entries =
        [
            new() { Id = 1, Sid = "s1", Unsuffixed = "x:a" },
            new() { Id = 5, Sid = "s2", Unsuffixed = "x:a", HasSuffix = true },
        ];

        Assert.Equal("x:a", UidResolver.TryResolve(entries, "x:a", "s1",
            true, out _));
        Assert.Equal("x:a#5", UidResolver.TryResolve(entries, "x:a", "s2",
            true, out _));
        Assert.Equal("x:a#5", UidResolver.TryResolve(entries, "x:a", "s2",
            false, out _));
    }

    [Fact]
    public void UidResolver_OtherSid_NotUnique_Shared()
    {
        UidEntry[] entries = [new() { Id = 1, Sid = "s1", Unsuffixed = "x:a" }];

        Assert.Equal("x:a", UidResolver.TryResolve(entries, "x:a", "s2",
            false, out _));
    }

    [Fact]
    public void UidResolver_OtherSid_Unique_NewSuffixed()
    {
        UidEntry[] entries = [new() { Id = 1, Sid = "s1", Unsuffixed = "x:a" }];

        Assert.Null(UidResolver.TryResolve(entries, "x:a", "s2", true,
            out bool hasSuffix));
        Assert.True(hasSuffix);
    }

    [Fact]
    public void RamUidBuilder_SameSidUnique_SameUid()
    {
        // a source saved again must get back the same UID, rather than
        // a new suffix at each save
        RamUidBuilder builder = new();
        builder.BuildUid("x:ts##", "s1");
        string uid2 = builder.BuildUid("x:ts##", "s2");

        string uid2Again = builder.BuildUid("x:ts##", "s2");
        string uid1Again = builder.BuildUid("x:ts##", "s1");

        Assert.Equal(uid2, uid2Again);
        Assert.Equal("x:ts", uid1Again);
    }

    [Fact]
    public void RamUidBuilder_CaseInsensitive()
    {
        RamUidBuilder builder = new();
        builder.BuildUid("x:TS##", "s1");

        string uid = builder.BuildUid("x:ts##", "s2");

        Assert.StartsWith("x:ts#", uid);
    }

    [Fact]
    public void UidEntry_ToString_NoSuffix()
    {
        UidEntry entry = new() { Id = 3, Unsuffixed = "x:a" };
        Assert.Equal("x:a", entry.ToString());
    }
    #endregion

    #region GraphUpdater
    private const string PART_TYPE = "it.vedph.historical-events";

    private static GraphNodeMapping GetEventsMapping(int? flags = null) => new()
    {
        Name = "events",
        SourceType = Node.SOURCE_PART,
        PartTypeFilter = PART_TYPE,
        FlagsFilter = flags,
        Source = "events",
        Sid = "{$part-id}/{@eid}",
        Output = new NodeMappingOutput
        {
            Nodes = new Dictionary<string, MappedNode>
            {
                ["event"] = new MappedNode
                {
                    Uid = "x:events/{@eid}",
                    Label = "event {@eid}",
                    Tag = "event"
                }
            },
            Triples =
            [
                new MappedTriple
                {
                    S = "{?event}",
                    P = "a",
                    O = "crm:e5_event"
                }
            ]
        }
    };

    private static (IItem Item, HistoricalEventsPart Part) GetItemAndPart(
        int flags = 0)
    {
        Item item = new()
        {
            Title = "Petrarch",
            FacetId = "person",
            Flags = flags,
            UserId = "zeus",
            CreatorId = "zeus"
        };
        HistoricalEventsPart part = new()
        {
            ItemId = item.Id,
            UserId = "zeus",
            CreatorId = "zeus"
        };
        part.Events.Add(new HistoricalEvent { Eid = "birth", Type = "birth" });
        return (item, part);
    }

    [Fact]
    public async Task UpdateAsync_Part_SetHasSourceIdAndNodeTag()
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetEventsMapping());
        (IItem item, HistoricalEventsPart part) = GetItemAndPart();
        GraphUpdater updater = new(repository);

        await updater.UpdateAsync(item, part);

        GraphSet set = Assert.Single(repository.UpdatedSets);
        Assert.Contains(part.Id, set.SourceIds);
        UriNode node = Assert.Single(set.Nodes);
        Assert.Equal("x:events/birth", node.Uri);
        Assert.Equal("event birth", node.Label);
        // the tag defined in the mapping is applied to the node
        Assert.Equal("event", node.Tag);
        Assert.Equal($"{part.Id}/birth", node.Sid);
        UriTriple triple = Assert.Single(set.Triples);
        Assert.Equal("rdf:type", triple.PredicateUri);
        // metadata are reset after each update
        Assert.Empty(updater.Metadata);
    }

    [Fact]
    public void Update_NoMatchingMappings_EmptySetWithSourceId()
    {
        // when nothing gets generated, the set must still tell the
        // repository which source it represents, so that whatever was
        // previously generated from it gets removed
        MockGraphRepository repository = new();
        (IItem item, HistoricalEventsPart part) = GetItemAndPart();
        GraphUpdater updater = new(repository);

        updater.Update(item, part);

        GraphSet set = Assert.Single(repository.UpdatedSets);
        Assert.Empty(set.Nodes);
        Assert.Empty(set.Triples);
        Assert.Contains(part.Id, set.SourceIds);
    }

    [Fact]
    public void Update_Item_SetHasItemId()
    {
        MockGraphRepository repository = new();
        (IItem item, _) = GetItemAndPart();
        GraphUpdater updater = new(repository);

        updater.Update(item);

        GraphSet set = Assert.Single(repository.UpdatedSets);
        Assert.Contains(item.Id, set.SourceIds);
    }

    [Theory]
    // mapping requires flag 1: item with flags 1 or 3 matches
    [InlineData(1, 1, true)]
    [InlineData(1, 3, true)]
    // item without flag 1 does not match
    [InlineData(1, 2, false)]
    [InlineData(1, 0, false)]
    // mapping requires flags 1+2
    [InlineData(3, 1, false)]
    [InlineData(3, 7, true)]
    public void Update_FlagsFilter_ItemMustHaveAllMappingFlags(
        int mappingFlags, int itemFlags, bool matched)
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetEventsMapping(mappingFlags));
        (IItem item, HistoricalEventsPart part) = GetItemAndPart(itemFlags);
        GraphUpdater updater = new(repository);

        updater.Update(item, part);

        GraphSet set = Assert.Single(repository.UpdatedSets);
        Assert.Equal(matched ? 1 : 0, set.Nodes.Count);
    }

    [Fact]
    public void Explain_ReportsAppliedMappings()
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetEventsMapping());
        (IItem item, HistoricalEventsPart part) = GetItemAndPart();
        GraphUpdater updater = new(repository);

        GraphUpdaterExplanation? explanation = updater.Explain(item, part);

        Assert.NotNull(explanation);
        Assert.Single(explanation.Set.Nodes);
        Assert.NotEmpty(explanation.Mappings);
        // explaining does not update the graph
        Assert.Empty(repository.UpdatedSets);
        Assert.Empty(updater.Metadata);
    }

    internal static GraphNodeMapping GetTimespansMapping() => new()
    {
        Name = "timespans",
        SourceType = Node.SOURCE_PART,
        PartTypeFilter = PART_TYPE,
        Source = "events[0].chronotopes",
        // the same SID for all the chronotopes of the event
        Sid = "{$part-id}/chronotopes",
        Output = new NodeMappingOutput
        {
            Nodes = new Dictionary<string, MappedNode>
            {
                ["ts"] = new MappedNode { Uid = "x:timespans/ts##" }
            }
        }
    };

    internal static (IItem Item, HistoricalEventsPart Part)
        GetItemAndPartWithChronotopes()
    {
        (IItem item, HistoricalEventsPart part) = GetItemAndPart();
        part.Events[0].Chronotopes =
        [
            new Cadmus.Refs.Bricks.AssertedChronotope
            {
                Place = new Cadmus.Refs.Bricks.AssertedPlace { Value = "a" }
            },
            new Cadmus.Refs.Bricks.AssertedChronotope
            {
                Place = new Cadmus.Refs.Bricks.AssertedPlace { Value = "b" }
            }
        ];
        return (item, part);
    }

    [Fact]
    public void Update_SameUniqueUidAndSidTwice_DistinctAndStable()
    {
        MockGraphRepository repository = new();
        repository.AddMapping(GetTimespansMapping());
        (IItem item, HistoricalEventsPart part) =
            GetItemAndPartWithChronotopes();
        GraphUpdater updater = new(repository);

        updater.Update(item, part);
        updater.Update(item, part);

        string[] first = [.. repository.UpdatedSets[0].Nodes.Select(n => n.Uri!)];
        string[] second = [.. repository.UpdatedSets[1].Nodes.Select(n => n.Uri!)];
        Assert.Equal(2, first.Distinct().Count());
        Assert.Equal(first, second);
        Assert.Equal("x:timespans/ts", first[0]);
    }

    [Fact]
    public void ItemFlusher_Dispose_EmptyQueue_NoFlush()
    {
        int flushes = 0;
        using (ItemFlusher<int> flusher = new(_ => flushes++, 2))
        {
            flusher.Add(1);
            flusher.Add(2);
        }
        Assert.Equal(1, flushes);
    }

    [Fact]
    public void ItemFlusher_Dispose_FlushesRemaining()
    {
        List<int> flushed = [];
        using (ItemFlusher<int> flusher = new(items => flushed.AddRange(items),
            2))
        {
            flusher.Add(1);
            flusher.Add(2);
            flusher.Add(3);
        }
        Assert.Equal([1, 2, 3], flushed.Order());
    }
    #endregion
}
