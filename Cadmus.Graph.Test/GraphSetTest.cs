using System;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class GraphSetTest
{
    [Fact]
    public void Ctor_Default_EmptyLists()
    {
        GraphSet set = new();

        Assert.Empty(set.Nodes);
        Assert.Empty(set.Triples);
    }

    [Fact]
    public void Ctor_WithLists_NullNodes_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GraphSet(null!, []));
    }

    [Fact]
    public void Ctor_WithLists_NullTriples_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GraphSet([], null!));
    }

    [Fact]
    public void AddNodes_Null_Throws()
    {
        GraphSet set = new();

        Assert.Throws<ArgumentNullException>(() => set.AddNodes(null!));
    }

    [Fact]
    public void AddNodes_NewNodes_AreAdded()
    {
        GraphSet set = new();

        set.AddNodes([new UriNode { Id = 1 }, new UriNode { Id = 2 }]);

        Assert.Equal(2, set.Nodes.Count);
    }

    [Fact]
    public void AddNodes_DuplicateId_IsSkipped()
    {
        GraphSet set = new();
        set.AddNodes([new UriNode { Id = 1, Label = "first" }]);

        set.AddNodes([new UriNode { Id = 1, Label = "second" }]);

        Assert.Single(set.Nodes);
        Assert.Equal("first", set.Nodes[0].Label);
    }

    [Fact]
    public void AddTriples_Null_Throws()
    {
        GraphSet set = new();

        Assert.Throws<ArgumentNullException>(() => set.AddTriples(null!));
    }

    [Fact]
    public void AddTriples_NewTriples_AreAdded()
    {
        GraphSet set = new();

        set.AddTriples(
        [
            new UriTriple { SubjectId = 1, PredicateId = 2, ObjectId = 3 }
        ]);

        Assert.Single(set.Triples);
    }

    [Fact]
    public void AddTriples_DuplicateTriple_IsSkipped()
    {
        GraphSet set = new();
        set.AddTriples(
        [
            new UriTriple { SubjectId = 1, PredicateId = 2, ObjectId = 3 }
        ]);

        set.AddTriples(
        [
            new UriTriple { SubjectId = 1, PredicateId = 2, ObjectId = 3 }
        ]);

        Assert.Single(set.Triples);
    }

    [Fact]
    public void AddTriples_DifferentObject_IsAdded()
    {
        GraphSet set = new();
        set.AddTriples(
        [
            new UriTriple { SubjectId = 1, PredicateId = 2, ObjectId = 3 }
        ]);

        set.AddTriples(
        [
            new UriTriple { SubjectId = 1, PredicateId = 2, ObjectId = 4 }
        ]);

        Assert.Equal(2, set.Triples.Count);
    }

    [Fact]
    public void GetNodesByGuid_GroupsBySidPrefix()
    {
        const string guid1 = "76066733-6f81-48dd-a653-284d5be54cfb";
        const string guid2 = "aaaaaaaa-6f81-48dd-a653-284d5be54cfb";
        GraphSet set = new();
        set.Nodes.Add(new UriNode { Id = 1, Sid = guid1 });
        set.Nodes.Add(new UriNode { Id = 2, Sid = guid1 + "/child" });
        set.Nodes.Add(new UriNode { Id = 3, Sid = guid2 });
        set.Nodes.Add(new UriNode { Id = 4, Sid = null });

        IDictionary<string, IList<UriNode>> groups = set.GetNodesByGuid();

        Assert.Equal(3, groups.Count);
        Assert.Equal(2, groups[guid1].Count);
        Assert.Single(groups[guid2]);
        Assert.Single(groups[""]);
    }

    [Fact]
    public void GetTriplesByGuid_GroupsBySidPrefix()
    {
        const string guid1 = "76066733-6f81-48dd-a653-284d5be54cfb";
        GraphSet set = new();
        set.Triples.Add(new UriTriple
        {
            SubjectId = 1,
            PredicateId = 2,
            ObjectId = 3,
            Sid = guid1
        });
        set.Triples.Add(new UriTriple
        {
            SubjectId = 4,
            PredicateId = 5,
            ObjectId = 6,
            Sid = null
        });

        IDictionary<string, IList<UriTriple>> groups = set.GetTriplesByGuid();

        Assert.Equal(2, groups.Count);
        Assert.Single(groups[guid1]);
        Assert.Single(groups[""]);
    }

    [Fact]
    public void ToString_ReportsCounts()
    {
        GraphSet set = new();
        set.Nodes.Add(new UriNode { Id = 1 });
        set.Triples.Add(new UriTriple
        {
            SubjectId = 1,
            PredicateId = 2,
            ObjectId = 3
        });

        Assert.Equal("N: 1 | T: 1", set.ToString());
    }
}
