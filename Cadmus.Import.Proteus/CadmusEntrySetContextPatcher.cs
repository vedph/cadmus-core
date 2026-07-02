using Cadmus.Core;
using Fusi.Tools.Configuration;
using Proteus.Core.Regions;
using Proteus.Entries.Export;
using System;
using System.Threading.Tasks;

namespace Cadmus.Import.Proteus;

/// <summary>
/// Cadmus entry set context patcher. This patcher is used to patch the items
/// in the context to ensure that they have facet ID, title, description,
/// sort key, creator ID, and user ID.
/// <para>Tag: <c>it.vedph.entry-set-context-patcher.cadmus</c>.</para>
/// </summary>
[Tag("it.vedph.entry-set-context-patcher.cadmus")]
public class CadmusEntrySetContextPatcher : EntrySetContextPatcher,
    IEntrySetContextPatcher
{
    private readonly StandardItemSortKeyBuilder _sortKeyBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadmusEntrySetContextPatcher"/>
    /// class.
    /// </summary>
    public CadmusEntrySetContextPatcher()
    {
        _sortKeyBuilder = new StandardItemSortKeyBuilder();
    }

    /// <summary>
    /// Patches the specified context.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <exception cref="ArgumentNullException">context</exception>
    protected override Task DoPatchAsync(IEntrySetContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        CadmusEntrySetContext ctx = (CadmusEntrySetContext)context;
        foreach (IItem item in ctx.Items)
        {
            if (string.IsNullOrEmpty(item.FacetId)) item.FacetId = "default";

            if (string.IsNullOrEmpty(item.Title)) item.Title = item.Id;

            item.Description ??= "";

            if (string.IsNullOrEmpty(item.SortKey))
                item.SortKey = _sortKeyBuilder.BuildKey(item, null);

            if (string.IsNullOrEmpty(item.CreatorId))
                item.CreatorId = "zeus";

            if (string.IsNullOrEmpty(item.UserId))
                item.UserId = "zeus";
        }
        return Task.CompletedTask;
    }
}
