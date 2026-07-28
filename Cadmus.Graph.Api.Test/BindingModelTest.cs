using System.Collections.Generic;
using Cadmus.Graph.Api.Models;
using Xunit;

namespace Cadmus.Graph.Api.Test;

public sealed class BindingModelTest
{
    [Fact]
    public void NodeFilterBindingModel_ToNodeFilter_CopiesAllProperties()
    {
        NodeFilterBindingModel model = new()
        {
            PageNumber = 2,
            PageSize = 20,
            Uid = "x:sample",
            IsClass = true,
            Tag = "tag1",
            Label = "label1",
            SourceType = 1,
            Sid = "sid1",
            IsSidPrefix = true,
            ClassIds = [1, 2],
            LinkedNodeId = 5,
            LinkedNodeRole = 'S'
        };

        NodeFilter filter = model.ToNodeFilter();

        Assert.Equal(2, filter.PageNumber);
        Assert.Equal(20, filter.PageSize);
        Assert.Equal("x:sample", filter.Uid);
        Assert.True(filter.IsClass);
        Assert.Equal("tag1", filter.Tag);
        Assert.Equal("label1", filter.Label);
        Assert.Equal(1, filter.SourceType);
        Assert.Equal("sid1", filter.Sid);
        Assert.True(filter.IsSidPrefix);
        Assert.Equal([1, 2], filter.ClassIds);
        Assert.Equal(5, filter.LinkedNodeId);
        Assert.Equal('S', filter.LinkedNodeRole);
    }

    [Fact]
    public void TripleFilterBindingModel_ToTripleFilter_CopiesAllProperties()
    {
        TripleFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            SubjectId = 1,
            PredicateIds = new HashSet<int> { 2, 3 },
            NotPredicateIds = new HashSet<int> { 4 },
            HasLiteralObject = true,
            ObjectId = 5,
            Sid = "sid1",
            IsSidPrefix = true,
            Tag = "tag1"
        };

        TripleFilter filter = model.ToTripleFilter();

        Assert.Equal(1, filter.PageNumber);
        Assert.Equal(10, filter.PageSize);
        Assert.Equal(1, filter.SubjectId);
        Assert.Equal(new HashSet<int> { 2, 3 }, filter.PredicateIds);
        Assert.Equal(new HashSet<int> { 4 }, filter.NotPredicateIds);
        Assert.True(filter.HasLiteralObject);
        Assert.Equal(5, filter.ObjectId);
        Assert.Equal("sid1", filter.Sid);
        Assert.True(filter.IsSidPrefix);
        Assert.Equal("tag1", filter.Tag);
    }

    [Fact]
    public void LinkedNodeFilterBindingModel_ToLinkedNodeFilter_CopiesAllProperties()
    {
        LinkedNodeFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            Uid = "x:sample",
            IsClass = false,
            Tag = "tag1",
            Label = "label1",
            SourceType = 2,
            Sid = "sid1",
            IsSidPrefix = true,
            ClassIds = [9],
            OtherNodeId = 7,
            PredicateId = 8,
            IsObject = true
        };

        LinkedNodeFilter filter = model.ToLinkedNodeFilter();

        Assert.Equal("x:sample", filter.Uid);
        Assert.False(filter.IsClass);
        Assert.Equal("tag1", filter.Tag);
        Assert.Equal("label1", filter.Label);
        Assert.Equal(2, filter.SourceType);
        Assert.Equal("sid1", filter.Sid);
        Assert.True(filter.IsSidPrefix);
        Assert.Equal([9], filter.ClassIds);
        Assert.Equal(7, filter.OtherNodeId);
        Assert.Equal(8, filter.PredicateId);
        Assert.True(filter.IsObject);
    }

    [Fact]
    public void LinkedLiteralFilterBindingModel_ToLinkedLiteralFilter_CopiesAllProperties()
    {
        LinkedLiteralFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            LiteralPattern = "^a",
            LiteralType = "xs:string",
            LiteralLanguage = "en",
            MinLiteralNumber = 1.5,
            MaxLiteralNumber = 9.5,
            SubjectId = 3,
            PredicateId = 4
        };

        LinkedLiteralFilter filter = model.ToLinkedLiteralFilter();

        Assert.Equal("^a", filter.LiteralPattern);
        Assert.Equal("xs:string", filter.LiteralType);
        Assert.Equal("en", filter.LiteralLanguage);
        Assert.Equal(1.5, filter.MinLiteralNumber);
        Assert.Equal(9.5, filter.MaxLiteralNumber);
        Assert.Equal(3, filter.SubjectId);
        Assert.Equal(4, filter.PredicateId);
    }
}
