using System;
using System.Collections.Generic;
using Cadmus.Graph.Api.Controllers;
using Cadmus.Graph.Api.Models;
using Fusi.Tools.Data;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Cadmus.Graph.Api.Test;

public sealed class GraphControllerTest
{
    [Fact]
    public void Ctor_NullRepository_Throws()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GraphController(null!));
    }

    [Fact]
    public void GetNodes_DelegatesToRepositoryWithConvertedFilter()
    {
        MockGraphRepository repository = new()
        {
            NodesResult = new DataPage<UriNode>(1, 10, 1,
                [new UriNode { Id = 1, Label = "n1" }])
        };
        GraphController controller = new(repository);
        NodeFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            Label = "n1"
        };

        DataPage<UriNode> result = controller.GetNodes(model);

        Assert.Equal(1, result.Total);
        Assert.NotNull(repository.LastNodeFilter);
        Assert.Equal("n1", repository.LastNodeFilter!.Label);
    }

    [Fact]
    public void GetNode_Existing_ReturnsOk()
    {
        MockGraphRepository repository = new()
        {
            NodeResult = new UriNode { Id = 5, Label = "n5" }
        };
        GraphController controller = new(repository);

        ActionResult result = controller.GetNode(5);

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(5, repository.LastNodeId);
        UriNode node = Assert.IsType<UriNode>(ok.Value);
        Assert.Equal("n5", node.Label);
    }

    [Fact]
    public void GetNode_NotExisting_ReturnsNotFound()
    {
        MockGraphRepository repository = new()
        {
            NodeResult = null
        };
        GraphController controller = new(repository);

        ActionResult result = controller.GetNode(123);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetNodeSet_DelegatesToRepository()
    {
        MockGraphRepository repository = new()
        {
            NodeSetResult = [new UriNode { Id = 1 }, null]
        };
        GraphController controller = new(repository);
        IList<int> ids = [1, 2];

        IList<UriNode?> result = controller.GetNodeSet(ids);

        Assert.Same(ids, repository.LastNodeIds);
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void GetNodeByUri_Existing_ReturnsOk()
    {
        MockGraphRepository repository = new()
        {
            NodeByUriResult = new UriNode { Id = 1, Uri = "x:sample" }
        };
        GraphController controller = new(repository);

        ActionResult result = controller.GetNodeByUri("x:sample");

        OkObjectResult ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("x:sample", repository.LastUri);
        Assert.Equal("x:sample", ((UriNode)ok.Value!).Uri);
    }

    [Fact]
    public void GetNodeByUri_NotExisting_ReturnsNotFound()
    {
        MockGraphRepository repository = new()
        {
            NodeByUriResult = null
        };
        GraphController controller = new(repository);

        ActionResult result = controller.GetNodeByUri("x:missing");

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void GetTripleGroups_UsesDefaultSortWhenNotSpecified()
    {
        MockGraphRepository repository = new();
        GraphController controller = new(repository);
        TripleFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10
        };

        controller.GetTripleGroups(model);

        Assert.Equal("Cu", repository.LastSort);
    }

    [Fact]
    public void GetTripleGroups_UsesProvidedSort()
    {
        MockGraphRepository repository = new();
        GraphController controller = new(repository);
        TripleFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            Sort = "Uc"
        };

        controller.GetTripleGroups(model);

        Assert.Equal("Uc", repository.LastSort);
    }

    [Fact]
    public void GetLinkedNodes_DelegatesToRepositoryWithConvertedFilter()
    {
        MockGraphRepository repository = new();
        GraphController controller = new(repository);
        LinkedNodeFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            OtherNodeId = 7,
            PredicateId = 8,
            IsObject = true
        };

        controller.GetLinkedNodes(model);

        Assert.NotNull(repository.LastLinkedNodeFilter);
        Assert.Equal(7, repository.LastLinkedNodeFilter!.OtherNodeId);
        Assert.Equal(8, repository.LastLinkedNodeFilter.PredicateId);
        Assert.True(repository.LastLinkedNodeFilter.IsObject);
    }

    [Fact]
    public void GetLinkedLiterals_DelegatesToRepositoryWithConvertedFilter()
    {
        MockGraphRepository repository = new();
        GraphController controller = new(repository);
        LinkedLiteralFilterBindingModel model = new()
        {
            PageNumber = 1,
            PageSize = 10,
            SubjectId = 3,
            PredicateId = 4,
            LiteralPattern = "^a"
        };

        controller.GetLinkedLiterals(model);

        Assert.NotNull(repository.LastLinkedLiteralFilter);
        Assert.Equal(3, repository.LastLinkedLiteralFilter!.SubjectId);
        Assert.Equal(4, repository.LastLinkedLiteralFilter.PredicateId);
        Assert.Equal("^a", repository.LastLinkedLiteralFilter.LiteralPattern);
    }
}
