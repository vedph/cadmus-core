using System;
using Xunit;

namespace Cadmus.Graph.Test;

public sealed class PocoTest
{
    [Fact]
    public void CadmusGraphException_Default_Ok()
    {
        CadmusGraphException ex = new();

        Assert.NotNull(ex);
    }

    [Fact]
    public void CadmusGraphException_Message_Ok()
    {
        CadmusGraphException ex = new("error");

        Assert.Equal("error", ex.Message);
    }

    [Fact]
    public void CadmusGraphException_MessageAndInner_Ok()
    {
        Exception inner = new("inner error");

        CadmusGraphException ex = new("error", inner);

        Assert.Equal("error", ex.Message);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void NamespaceEntry_ToString_Ok()
    {
        NamespaceEntry entry = new()
        {
            Prefix = "x",
            Uri = "http://www.sample.com"
        };

        Assert.Equal("x=http://www.sample.com", entry.ToString());
    }

    [Fact]
    public void Property_ToString_MinimalOk()
    {
        Property property = new() { Id = 12 };

        Assert.Equal("#12", property.ToString());
    }

    [Fact]
    public void Property_ToString_FullOk()
    {
        Property property = new()
        {
            Id = 12,
            DataType = "xs:string",
            LiteralEditor = "qed.md"
        };

        Assert.Equal("#12 ^xs:string [qed.md]", property.ToString());
    }

    [Fact]
    public void UriProperty_ToString_UsesUriWhenPresent()
    {
        UriProperty property = new()
        {
            Id = 12,
            Uri = "x:properties/comment"
        };

        Assert.Equal("x:properties/comment", property.ToString());
    }

    [Fact]
    public void UriProperty_ToString_FallsBackToBaseWhenUriMissing()
    {
        UriProperty property = new() { Id = 12 };

        Assert.Equal("#12", property.ToString());
    }

    [Fact]
    public void Triple_ToString_WithObjectId_Ok()
    {
        Triple triple = new()
        {
            Id = 1,
            SubjectId = 2,
            PredicateId = 3,
            ObjectId = 4
        };

        Assert.Equal("#1: #2 - #3 - #4", triple.ToString());
    }

    [Fact]
    public void Triple_ToString_WithLiteral_Ok()
    {
        Triple triple = new()
        {
            Id = 1,
            SubjectId = 2,
            PredicateId = 3,
            ObjectId = 0,
            ObjectLiteral = "hello"
        };

        Assert.Equal("#1: #2 - #3 - hello", triple.ToString());
    }

    [Fact]
    public void UriTriple_ToString_WithObjectUri_Ok()
    {
        UriTriple triple = new()
        {
            SubjectUri = "x:s",
            PredicateUri = "x:p",
            ObjectUri = "x:o"
        };

        Assert.Equal("x:s - #x:p - x:o", triple.ToString());
    }

    [Fact]
    public void UriTriple_ToString_WithLiteral_Ok()
    {
        UriTriple triple = new()
        {
            SubjectUri = "x:s",
            PredicateUri = "x:p",
            ObjectLiteral = "hello"
        };

        Assert.Equal("x:s - #x:p - hello", triple.ToString());
    }

    [Fact]
    public void TripleGroup_ToString_Ok()
    {
        TripleGroup group = new()
        {
            PredicateId = 5,
            PredicateUri = "rdf:type",
            Count = 3
        };

        Assert.Equal("#5 rdf:type=3", group.ToString());
    }

    [Fact]
    public void UidEntry_ToString_NoSuffix_Ok()
    {
        UidEntry entry = new()
        {
            Id = 7,
            Unsuffixed = "x:timespans/ts",
            HasSuffix = false
        };

        Assert.Equal("x:timespans/ts", entry.ToString());
    }

    [Fact]
    public void UidEntry_ToString_WithSuffix_Ok()
    {
        UidEntry entry = new()
        {
            Id = 7,
            Unsuffixed = "x:timespans/ts",
            HasSuffix = true
        };

        Assert.Equal("x:timespans/ts7", entry.ToString());
    }

    [Fact]
    public void UidEntry_Sid_RoundTrips()
    {
        UidEntry entry = new() { Sid = "guid1" };

        Assert.Equal("guid1", entry.Sid);
    }

    [Fact]
    public void NodeMappingOutput_HasXxx_ReflectCollectionState()
    {
        NodeMappingOutput output = new();

        Assert.False(output.HasNodes);
        Assert.False(output.HasTriples);
        Assert.False(output.HasMetadata);
        Assert.True(output.HasNoGraph);

        output.Nodes["n"] = new MappedNode { Uid = "x:n" };
        Assert.True(output.HasNodes);
        Assert.False(output.HasNoGraph);

        output.Triples.Add(new MappedTriple { S = "s", P = "p", O = "o" });
        Assert.True(output.HasTriples);

        output.Metadata["m"] = "v";
        Assert.True(output.HasMetadata);
    }

    [Fact]
    public void NodeMappingOutput_ToString_Ok()
    {
        NodeMappingOutput output = new();
        output.Nodes["n"] = new MappedNode { Uid = "x:n" };
        output.Triples.Add(new MappedTriple { S = "s", P = "p", O = "o" });
        output.Metadata["m"] = "v";

        Assert.Equal("N=1, T=1, M=1", output.ToString());
    }

    [Fact]
    public void GraphUpdaterExplanation_Ctor_InitializesDefaults()
    {
        GraphUpdaterExplanation explanation = new();

        Assert.NotNull(explanation.Filter);
        Assert.Empty(explanation.Mappings);
        Assert.Empty(explanation.Metadata);
        Assert.NotNull(explanation.Set);
    }

    [Fact]
    public void GraphUpdaterExplanation_ToString_ReportsCounts()
    {
        GraphUpdaterExplanation explanation = new();
        explanation.Mappings.Add(new NodeMapping { Name = "m1" });
        explanation.Metadata["k"] = "v";

        Assert.Equal("Mappings=1, Metadata=1", explanation.ToString());
    }
}
