using Cadmus.Api.Models.Graph;
using Cadmus.Graph;
using CadmusApi.Services;
using Fusi.Tools.Data;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;

namespace CadmusApi.Controllers;

[ApiController]
[Route("api/demo/graph")]
public sealed class WalkerDemoGraphController : ControllerBase
{
    private readonly WalkerDemoGraphRepository _repository;

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphController" />
    /// class.
    /// <paramref name="repository"/>The repository to use.</paramref>
    public WalkerDemoGraphController(WalkerDemoGraphRepository repository)
    {
        _repository = repository ??
            throw new ArgumentNullException(nameof(repository));
    }

    #region Nodes
    /// <summary>
    /// Get the specified page of graph nodes.
    /// </summary>
    /// <param name="model">The filter.</param>
    /// <returns>A page of nodes.</returns>
    [HttpGet("nodes")]
    [ProducesResponseType(200)]
    public DataPage<UriNode> GetDemoNodes([FromQuery]
        NodeFilterBindingModel model)
    {
        return _repository.GetNodes(model.ToNodeFilter());
    }

    /// <summary>
    /// Get the node with the specified ID.
    /// </summary>
    /// <param name="id">The node ID.</param>
    /// <returns>Node.</returns>
    [HttpGet("nodes/{id}", Name = "GetDemoNode")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public ActionResult<UriNode> GetDemoNode(int id)
    {
        UriNode? node = _repository.GetNode(id);
        if (node == null) return NotFound();
        return Ok(node);
    }

    /// <summary>
    /// Get the specified set of nodes.
    /// </summary>
    /// <param name="ids">The IDs of the nodes to get.</param>
    /// <returns>Node.</returns>
    [HttpGet("nodes-set")]
    [ProducesResponseType(200)]
    public IList<UriNode> GetDemoNodeSet([FromQuery] IList<int> ids)
    {
        return _repository.GetNodes(ids)!;
    }

    [HttpGet("nodes-by-uri")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public ActionResult GetDemoNodeByUri([FromQuery] string uri)
    {
        UriNode? node = _repository.GetNodeByUri(uri);
        if (node == null) return NotFound();
        return Ok(node);
    }

    [HttpGet("walk/triples")]
    [ProducesResponseType(200)]
    public DataPage<TripleGroup> GetDemoTripleGroups([FromQuery]
        TripleFilterBindingModel model)
    {
        return _repository.GetTripleGroups(
            model.ToTripleFilter(), model.Sort ?? "Cu");
    }

    [HttpGet("walk/nodes")]
    [ProducesResponseType(200)]
    public DataPage<UriNode> GetDemoLinkedNodes([FromQuery]
        LinkedNodeFilterBindingModel model)
    {
        return _repository.GetLinkedNodes(model.ToLinkedNodeFilter());
    }

    [HttpGet("walk/nodes/literal")]
    [ProducesResponseType(200)]
    public DataPage<UriTriple> GetDemoLinkedLiterals([FromQuery]
        LinkedLiteralFilterBindingModel model)
    {
        return _repository.GetLinkedLiterals(model.ToLinkedLiteralFilter());
    }

    /// <summary>
    /// Adds or updates the specified node.
    /// </summary>
    /// <param name="model">The node model.</param>
    [HttpPost("nodes")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public ActionResult AddDemoNode([FromBody] NodeBindingModel model)
    {
        UriNode node = new()
        {
            Id = _repository.AddUri(model.Uri!),
            IsClass = model.IsClass,
            Tag = model.Tag,
            Label = model.Label,
            SourceType = model.SourceType,
            Sid = model.Sid,
            Uri = model.Uri
        };
        _repository.AddNode(node);
        return CreatedAtRoute("GetNode", new { node.Id }, node);
    }

    /// <summary>
    /// Deletes the node with the specified ID.
    /// </summary>
    /// <param name="id">The node's ID.</param>
    [HttpDelete("nodes/{id}")]
    public void DeleteDemoNode(int id)
    {
        _repository.DeleteNode(id);
    }
    #endregion

    #region Triples
    /// <summary>
    /// Get the specified page of graph triples.
    /// </summary>
    /// <param name="model"></param>
    /// <returns>Page with triples.</returns>
    [HttpGet("triples")]
    [ProducesResponseType(200)]
    public DataPage<UriTriple> GetDemoTriples([FromQuery]
        TripleFilterBindingModel model)
    {
        return _repository.GetTriples(model.ToTripleFilter());
    }

    /// <summary>
    /// Get the triple with the specified ID.
    /// </summary>
    /// <param name="id">The triple ID.</param>
    /// <returns>Triple.</returns>
    [HttpGet("triples/{id}", Name = "GetDemoTriple")]
    [ProducesResponseType(200)]
    [ProducesResponseType(404)]
    public ActionResult<UriNode> GetDemoTriple(int id)
    {
        UriTriple? triple = _repository.GetTriple(id);
        if (triple == null) return NotFound();
        return Ok(triple);
    }

    /// <summary>
    /// Adds or updates the specified triple.
    /// </summary>
    /// <param name="model">The triple model.</param>
    [HttpPost("triples")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public ActionResult AddDemoTriple([FromBody] TripleBindingModel model)
    {
        Triple triple = new()
        {
            Id = model.Id,
            SubjectId = model.SubjectId,
            PredicateId = model.PredicateId,
            ObjectId = model.ObjectId,
            ObjectLiteral = model.ObjectLiteral,
            ObjectLiteralIx = model.ObjectLiteralIx,
            LiteralLanguage = model.LiteralLanguage,
            LiteralType = model.LiteralType,
            LiteralNumber = model.LiteralNumber,
            Sid = model.Sid,
            Tag = model.Tag
        };

        TripleObjectSupplier.Supply(triple, model.DefaultLanguage);

        _repository.AddTriple(triple);
        return CreatedAtRoute("GetTriple", new { triple.Id }, triple);
    }

    /// <summary>
    /// Deletes the triple with the specified ID.
    /// </summary>
    /// <param name="id">The triple's ID.</param>
    [HttpDelete("triples/{id}")]
    public void DeleteDemoTriple(int id)
    {
        _repository.DeleteTriple(id);
    }
    #endregion
}
