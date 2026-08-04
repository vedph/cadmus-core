using Cadmus.Export.Mapping;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Cadmus.Graph;

/// <summary>
/// JSON-based node mapper building RDF nodes and triples.
/// <para>Tag: <c>node-mapper.json</c>.</para>
/// </summary>
/// <seealso cref="JsonNodeMapper{TTarget}" />
/// <seealso cref="IGraphNodeMapper" />
[Tag("node-mapper.json")]
public sealed class JsonGraphNodeMapper : JsonNodeMapper<GraphSet>, IGraphNodeMapper
{
    public IUidBuilder UidBuilder { get; set; }

    /// <summary>
    /// Gets or sets the context nodes of this mapper. These are the nodes
    /// created during the mapping process, keyed under some arbitrary
    /// identifier defined in the mapping configuration.
    /// </summary>
    private readonly Dictionary<string, UriNode> _contextNodes;

    public JsonGraphNodeMapper()
    {
        // by default use a RAM-based builder
        UidBuilder = new RamUidBuilder();
        _contextNodes = [];
    }

    private string ResolveNode(string template)
    {
        // - {?node} or {?node:uri} => uri
        // - {?node:label} => label
        // - {?node:sid} => sid
        // - {?node:src_type} => source type
        string key;
        string? prop = null;
        int i = template.LastIndexOf(':');
        if (i > -1)
        {
            key = template[..i];
            prop = template[(i + 1)..];
        }
        else
        {
            key = template;
        }
        if (!_contextNodes.TryGetValue(key, out UriNode? node)) return "";
        return prop switch
        {
            "label" => node.Label ?? "",
            "sid" => node.Sid ?? "",
            "src_type" => node.SourceType.ToString(CultureInfo.InvariantCulture),
            _ => node.Uri ?? "",
        };
    }

    private string ResolveNode(TemplateNode node)
    {
        if (node.ChildrenCount == 0) return "";

        StringBuilder sb = new();
        foreach (TemplateNode child in node.Children
            .Where(child => child.Value != null))
        {
            sb.Append(child.Value);
        }

        string value = sb.ToString();
        switch (node.Type)
        {
            case TemplateNodeType.Node:
                return ResolveNode(value);

            case TemplateNodeType.Metadatum:
                if (Data.TryGetValue(value, out object? d))
                    return d?.ToString() ?? "";
                break;

            case TemplateNodeType.Expression:
                return ResolveDataExpression(value);

            case TemplateNodeType.Macro:
                return ResolveMacro(value);
        }
        return "";
    }

    /// <summary>
    /// Fill the specified template by resolving macros (<c>!{...}</c>),
    /// node placeholders (<c>?{...}</c>), metadata placeholders
    /// (<c>${...}</c>), and data expression placeholders <c>@{...}</c>.
    /// </summary>
    /// <param name="template">The template.</param>
    /// <param name="uidFilter">True to apply <see cref="UidFilter"/> to
    /// the result before returning it.</param>
    protected override string ResolveTemplate(string template, bool uidFilter)
    {
        ArgumentNullException.ThrowIfNull(template);

        TemplateTree tree = TemplateTree.Create(template);
        string resolved = tree.Resolve(ResolveNode);
        return uidFilter ? UidFilter.Apply(resolved) : resolved;
    }

    private void AddNodes(string sid, GraphNodeMapping mapping, GraphSet target)
    {
        foreach (var p in mapping.Output!.Nodes)
        {
            string uri = UidBuilder.BuildUid(ResolveTemplate(p.Value.Uid!, true),
                sid);
            UriNode node = new()
            {
                Uri = uri,
                SourceType = SourceType,
                Sid = sid,
                Label = string.IsNullOrEmpty(p.Value.Label) ?
                    uri : ResolveTemplate(p.Value.Label, false)
            };
            _contextNodes[p.Key] = node;
            target.Nodes.Add(node);
        }
    }

    private void AddTriples(string sid, GraphNodeMapping mapping, GraphSet target)
    {
        int n = 0;
        foreach (MappedTriple tripleSource in mapping.Output!.Triples)
        {
            n++;
            if (string.IsNullOrEmpty(tripleSource.S))
            {
                throw new CadmusGraphException(
                    $"Undefined triple subject at mapping #{n}: {mapping}");
            }
            if (string.IsNullOrEmpty(tripleSource.P))
            {
                throw new CadmusGraphException(
                    $"Undefined triple predicate at mapping #{n}: {mapping}");
            }
            if (string.IsNullOrEmpty(tripleSource.O) && tripleSource.OL == null)
            {
                throw new CadmusGraphException(
                    $"Undefined triple object at mapping #{n}: {mapping}");
            }

            UriTriple triple = new()
            {
                Sid = sid,
                SubjectUri = ResolveTemplate(tripleSource.S!, true),
                // P=a becomes rdf:type
                PredicateUri = ResolveTemplate(tripleSource.P == "a"
                    ? "rdf:type" : tripleSource.P!, true),
                ObjectUri = tripleSource.O != null
                    ? ResolveTemplate(tripleSource.O!, true) : null,
                ObjectLiteral = tripleSource.OL != null
                    ? ResolveTemplate(tripleSource.OL, false)
                    : null
            };
            LiteralHelper.AdjustLiteral(triple);
            target.Triples.Add(triple);
        }
    }

    /// <summary>
    /// Builds the RDF nodes and triples for the specified matched mapping.
    /// </summary>
    /// <param name="sid">The SID resolved for this mapping (which might be
    /// inherited from an ancestor mapping), or null/empty when none could
    /// be resolved.</param>
    /// <param name="mapping">The matched mapping.</param>
    /// <param name="target">The target graph set.</param>
    /// <exception cref="CadmusGraphException">Undefined SID for a mapping
    /// requiring one to emit nodes or triples.</exception>
    protected override void BuildOutput(string? sid, NodeMapping mapping,
        GraphSet target)
    {
        GraphNodeMapping gm = (GraphNodeMapping)mapping;
        if (gm.Output == null) return;

        // metadata
        if (gm.Output.HasMetadata)
        {
            foreach (var p in gm.Output.Metadata)
                Data[p.Key] = ResolveTemplate(p.Value!, false);
        }

        if (string.IsNullOrEmpty(sid))
        {
            if (!gm.Output.HasNoGraph)
            {
                throw new CadmusGraphException(
                    $"Undefined SID for mapping {mapping}");
            }
            return;
        }

        // nodes
        if (gm.Output.HasNodes) AddNodes(sid!, gm, target);
        // triples
        if (gm.Output.HasTriples) AddTriples(sid!, gm, target);
    }
}
