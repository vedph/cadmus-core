using System;
using System.Collections.Generic;
using System.Linq;
using Fusi.Tools;
using Fusi.Tools.Data;
using Fusi.Tools.Text;
using Proteus.Core.Text;
using Proteus.Rendering;
using Proteus.Rendering.Renderers;
using Proteus.Text.Plugs;

namespace Cadmus.Export.Renderers;

/// <summary>
/// Base class for <see cref="ICadmusTextTreeRenderer"/> implementations,
/// with filtering and grouping support.
/// </summary>
public abstract class CadmusTextTreeRenderer : GroupTextTreeRenderer<string>,
    ICadmusTextTreeRenderer
{
    private readonly TextFilterAdapter _adapter;

    /// <summary>
    /// Gets the optional filters to apply after the renderer completes.
    /// </summary>
    public List<ITextFilter> Filters { get; } = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="CadmusTextTreeRenderer"/>
    /// class.
    /// </summary>
    protected CadmusTextTreeRenderer()
    {
        _adapter = new(
        [
            new StringTextFilterPlug(),
                    new StringBuilderTextFilterPlug(),
                    new XElementTextFilterPlug(),
        ]);
    }

    /// <summary>
    /// Gets the group identifier from the specified context.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Group ID or null.</returns>
    protected override string? GetGroupId(IRendererContext context)
    {
        return context.Data.TryGetValue(ItemComposer.M_ITEM_GROUP, out object? id) ?
            id.ToString() : null;
    }

    /// <summary>
    /// Renders the tail of the output. This is called by the item composer
    /// once when ending the rendering process and can be used to output
    /// specific content at the document's end.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <returns>Tail content.</returns>
    public override string RenderTail(IRendererContext context)
    {
        // close a group if any
        if (GroupOrdinal > 0 && !string.IsNullOrEmpty(GroupTailTemplate))
        {
            return TextTemplate.FillTemplate(
                GroupTailTemplate, context.Data);
        }
        return "";
    }

    /// <summary>
    /// Wraps the received XML in the group head and tail templates, if any.
    /// </summary>
    /// <param name="xml">The XML.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Wrapped XML or just the input XML.</returns>
    protected string WrapXml(string xml, CadmusRendererContext context)
    {
        // if there is a pending group ID:
        // - if there is a current group, prepend tail.
        // - prepend head.
        if (PendingGroupId != null)
        {
            if (GroupOrdinal > 0 && !string.IsNullOrEmpty(GroupTailTemplate))
            {
                return TextTemplate.FillTemplate(
                    GroupTailTemplate, context.Data) + xml;
            }
            if (!string.IsNullOrEmpty(GroupHeadTemplate))
            {
                return TextTemplate.FillTemplate(
                    GroupHeadTemplate, context.Data) + xml;
            }
        }
        return xml;
    }

    /// <summary>
    /// Renders the specified JSON code.
    /// </summary>
    /// <param name="tree">The root node of the text tree.</param>
    /// <param name="context">The renderer context.</param>
    /// <returns>Rendered output.</returns>
    protected abstract string DoCadmusRender(TreeNode<ExportedSegment> tree,
        CadmusRendererContext context);

    /// <summary>
    /// Renders the specified tree.
    /// </summary>
    /// <param name="tree">tree</param>
    /// <param name="context">rendering context.</param>
    /// <returns>Result.</returns>
    protected override string? DoRender(TreeNode<ExportedSegment> tree,
        IRendererContext context)
    {
        string result = DoCadmusRender(tree, (CadmusRendererContext)context);

        // apply filters
        return ApplyFilters(result, context);
    }

    /// <summary>
    /// Applies the filters to the specified source object, returning a string
    /// with the result.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="context">The optional rendering context.</param>
    /// <returns>String.</returns>
    protected string ApplyFilters(object source,
        IHasDataDictionary? context = default)
    {
        // TODO: move to nested class and share across classes

        ArgumentNullException.ThrowIfNull(source);
        object? result = source;

        if (Filters.Count > 0)
        {
            foreach (ITextFilter filter in Filters.Where(f => !f.IsDisabled))
                result = filter.Apply(result, context);
        }

        return (string)(_adapter.Adapt(result, typeof(string), false) ?? "");
    }
}
