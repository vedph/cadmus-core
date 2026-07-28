using Xunit;

namespace Cadmus.Graph.Ef.PgSql.Test;

public sealed class EfPocoTest
{
    [Fact]
    public void EfNodeClass_ToString_Ok()
    {
        EfNodeClass nc = new()
        {
            NodeId = 1,
            ClassId = 2,
            Level = 3
        };

        Assert.Equal("1 a 2 @3", nc.ToString());
    }

    [Fact]
    public void EfMappingLink_ToString_Ok()
    {
        EfMappingLink link = new()
        {
            ParentId = 1,
            ChildId = 2
        };

        Assert.Equal("#1-2", link.ToString());
    }

    [Fact]
    public void EfNamespaceEntry_DefaultCtor_Ok()
    {
        EfNamespaceEntry entry = new();

        Assert.Equal("", entry.Id);
        Assert.Equal("", entry.Uri);
    }

    [Fact]
    public void EfNamespaceEntry_FromNamespaceEntry_Ok()
    {
        NamespaceEntry source = new()
        {
            Prefix = "x",
            Uri = "http://www.sample.com"
        };

        EfNamespaceEntry entry = new(source);

        Assert.Equal("x", entry.Id);
        Assert.Equal("http://www.sample.com", entry.Uri);
    }

    [Fact]
    public void EfNamespaceEntry_FromNull_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => new EfNamespaceEntry(null!));
    }

    [Fact]
    public void EfNamespaceEntry_ToNamespaceEntry_Ok()
    {
        EfNamespaceEntry entry = new()
        {
            Id = "x",
            Uri = "http://www.sample.com"
        };

        NamespaceEntry result = entry.ToNamespaceEntry();

        Assert.Equal("x", result.Prefix);
        Assert.Equal("http://www.sample.com", result.Uri);
    }

    [Fact]
    public void EfNamespaceEntry_ToString_Ok()
    {
        EfNamespaceEntry entry = new()
        {
            Id = "x",
            Uri = "http://www.sample.com"
        };

        Assert.Equal("x=http://www.sample.com", entry.ToString());
    }

    [Fact]
    public void EfUriEntry_DefaultCtor_Ok()
    {
        EfUriEntry entry = new();

        Assert.Equal("", entry.Uri);
    }

    [Fact]
    public void EfUriEntry_ToString_Ok()
    {
        EfUriEntry entry = new()
        {
            Id = 5,
            Uri = "x:sample"
        };

        Assert.Equal("#5 x:sample", entry.ToString());
    }

    [Fact]
    public void EfUidEntry_DefaultCtor_Ok()
    {
        EfUidEntry entry = new();

        Assert.Equal("", entry.Sid);
        Assert.Equal("", entry.Unsuffixed);
    }

    [Fact]
    public void EfUidEntry_ToString_NoSuffix_Ok()
    {
        EfUidEntry entry = new() { Id = 3, Unsuffixed = "x:ts" };

        Assert.Equal("#3 x:ts", entry.ToString());
    }

    [Fact]
    public void EfUidEntry_ToString_WithSuffix_Ok()
    {
        EfUidEntry entry = new()
        {
            Id = 3,
            Unsuffixed = "x:ts",
            HasSuffix = true
        };

        Assert.Equal("#3 x:ts*", entry.ToString());
    }

    [Fact]
    public void EfMappingMetaOutput_ToString_ShortValue_Ok()
    {
        EfMappingMetaOutput mo = new()
        {
            MappingId = 1,
            Id = 2,
            Name = "n",
            Value = "short"
        };

        Assert.Equal("#1.2: n=short", mo.ToString());
    }

    [Fact]
    public void EfMappingMetaOutput_ToString_LongValue_IsTruncated()
    {
        string longValue = new('x', 150);
        EfMappingMetaOutput mo = new()
        {
            MappingId = 1,
            Id = 2,
            Name = "n",
            Value = longValue
        };

        string s = mo.ToString();

        Assert.Equal($"#1.2: n={new string('x', 100)}", s);
    }

    [Fact]
    public void EfMappingNodeOutput_ToString_Ok()
    {
        EfMappingNodeOutput no = new()
        {
            MappingId = 1,
            Id = 2,
            Name = "event"
        };

        Assert.Equal("#1.2: event", no.ToString());
    }

    [Fact]
    public void EfMappingTripleOutput_ToString_WithUriObject_Ok()
    {
        EfMappingTripleOutput to = new()
        {
            S = "{?event}",
            P = "rdf:type",
            O = "crm:e7_activity"
        };

        Assert.Equal("{?event} rdf:type crm:e7_activity", to.ToString());
    }

    [Fact]
    public void EfMappingTripleOutput_ToString_WithLiteralObject_Ok()
    {
        EfMappingTripleOutput to = new()
        {
            S = "{?event}",
            P = "rdfs:label",
            O = "ignored-when-ol-present",
            OL = "\"a label\""
        };

        Assert.Equal("{?event} rdfs:label \"a label\"", to.ToString());
    }

    [Fact]
    public void EfProperty_DefaultCtor_Ok()
    {
        EfProperty property = new();

        Assert.Null(property.DataType);
    }

    [Fact]
    public void EfProperty_FromNull_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(
            () => new EfProperty(null!));
    }

    [Fact]
    public void EfProperty_FromProperty_Ok()
    {
        Property source = new()
        {
            Id = 7,
            DataType = "xs:string",
            LiteralEditor = "qed.md",
            Description = "desc"
        };

        EfProperty property = new(source);

        Assert.Equal(7, property.Id);
        Assert.Equal("xs:string", property.DataType);
        Assert.Equal("qed.md", property.LitEditor);
        Assert.Equal("desc", property.Description);
    }

    [Fact]
    public void EfProperty_ToUriProperty_NoNode_UriIsNull()
    {
        EfProperty property = new()
        {
            Id = 7,
            DataType = "xs:string"
        };

        UriProperty result = property.ToUriProperty();

        Assert.Equal(7, result.Id);
        Assert.Null(result.Uri);
    }

    [Fact]
    public void EfProperty_ToUriProperty_WithNode_UsesNodeUri()
    {
        EfProperty property = new()
        {
            Id = 7,
            Node = new EfNode
            {
                Id = 7,
                UriEntry = new EfUriEntry { Id = 7, Uri = "x:comment" }
            }
        };

        UriProperty result = property.ToUriProperty();

        Assert.Equal("x:comment", result.Uri);
    }

    [Fact]
    public void EfProperty_ToString_MinimalOk()
    {
        EfProperty property = new() { Id = 7 };

        Assert.Equal("#7", property.ToString());
    }

    [Fact]
    public void EfProperty_ToString_FullOk()
    {
        EfProperty property = new()
        {
            Id = 7,
            DataType = "xs:string",
            LitEditor = "qed.md"
        };

        Assert.Equal("#7 xs:string qed.md", property.ToString());
    }

    [Fact]
    public void EfNode_FromNode_Ok()
    {
        Node source = new()
        {
            Id = 9,
            IsClass = true,
            Tag = "tag1",
            Label = "label1",
            SourceType = Node.SOURCE_ITEM,
            Sid = "sid1"
        };

        EfNode node = new(source);

        Assert.Equal(9, node.Id);
        Assert.True(node.IsClass);
        Assert.Equal("tag1", node.Tag);
        Assert.Equal("label1", node.Label);
        Assert.Equal(Node.SOURCE_ITEM, node.SourceType);
        Assert.Equal("sid1", node.Sid);
    }

    [Fact]
    public void EfNode_ToUriNode_UsesExplicitUriOverUriEntry()
    {
        EfNode node = new()
        {
            Id = 9,
            Label = "label1",
            UriEntry = new EfUriEntry { Id = 9, Uri = "x:entry-uri" }
        };

        UriNode result = node.ToUriNode("x:explicit-uri");

        Assert.Equal("x:explicit-uri", result.Uri);
    }

    [Fact]
    public void EfNode_ToUriNode_FallsBackToUriEntry()
    {
        EfNode node = new()
        {
            Id = 9,
            Label = "label1",
            UriEntry = new EfUriEntry { Id = 9, Uri = "x:entry-uri" }
        };

        UriNode result = node.ToUriNode();

        Assert.Equal("x:entry-uri", result.Uri);
    }

    [Fact]
    public void EfMapping_FromNull_Throws()
    {
        Assert.Throws<System.ArgumentNullException>(() => new EfMapping(null!));
    }

    [Fact]
    public void EfMapping_ToString_AllFilters_Ok()
    {
        EfMapping mapping = new()
        {
            Id = 1,
            Name = "sample",
            SourceType = Node.SOURCE_ITEM,
            FacetFilter = "person",
            GroupFilter = "alpha.*",
            FlagsFilter = 0x10,
            TitleFilter = "title1",
            PartTypeFilter = "it.vedph.metadata",
            PartRoleFilter = "role1",
            Source = "events"
        };

        string s = mapping.ToString();

        Assert.Contains("#1 sample", s);
        Assert.Contains("facet=person", s);
        Assert.Contains("group=alpha.*", s);
        Assert.Contains("title=title1", s);
        Assert.Contains("type=it.vedph.metadata", s);
        Assert.Contains("role=role1", s);
        Assert.Contains(": events", s);
    }

    [Fact]
    public void EfMapping_ToString_WithOutputs_Ok()
    {
        EfMapping mapping = new()
        {
            Id = 1,
            Name = "sample",
            Source = "events",
            MetaOutputs = [new EfMappingMetaOutput { Name = "n", Value = "v" }],
            NodeOutputs = [new EfMappingNodeOutput { Name = "event", Uid = "x:e" }],
            TripleOutputs =
            [
                new EfMappingTripleOutput { S = "{?e}", P = "rdf:type", O = "x:c" }
            ]
        };

        string s = mapping.ToString();

        Assert.Contains("M=1", s);
        Assert.Contains("N=1", s);
        Assert.Contains("T=1", s);
    }

    [Fact]
    public void EfMapping_ToString_NoFilters_OmitsBrackets()
    {
        EfMapping mapping = new()
        {
            Id = 1,
            Name = "sample",
            Source = "events"
        };

        Assert.DoesNotContain("[", mapping.ToString());
    }

    [Fact]
    public void EfNode_NavigationProperties_RoundTrip()
    {
        EfNode node = new()
        {
            Id = 1,
            Property = new EfProperty { Id = 1 },
            Classes = [new EfNodeClass { NodeId = 1, ClassId = 2 }],
            SubjectTriples = [new EfTriple { Id = 1 }],
            PredicateTriples = [new EfTriple { Id = 2 }],
            ObjectTriples = [new EfTriple { Id = 3 }]
        };

        Assert.NotNull(node.Property);
        Assert.Single(node.Classes!);
        Assert.Single(node.SubjectTriples!);
        Assert.Single(node.PredicateTriples!);
        Assert.Single(node.ObjectTriples!);
    }

    [Fact]
    public void EfNodeClass_Node_RoundTrips()
    {
        EfNode node = new() { Id = 1 };

        EfNodeClass nc = new() { Node = node };

        Assert.Same(node, nc.Node);
    }

    [Fact]
    public void EfUriEntry_Node_RoundTrips()
    {
        EfNode node = new() { Id = 1 };

        EfUriEntry entry = new() { Node = node };

        Assert.Same(node, entry.Node);
    }

    [Fact]
    public void EfMappingTripleOutput_Mapping_RoundTrips()
    {
        EfMapping mapping = new() { Id = 1, Name = "m" };

        EfMappingTripleOutput to = new()
        {
            S = "s",
            P = "p",
            Mapping = mapping
        };

        Assert.Same(mapping, to.Mapping);
    }
}
