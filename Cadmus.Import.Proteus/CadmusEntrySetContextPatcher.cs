using Cadmus.Core;
using Fusi.Tools.Configuration;
using Proteus.Core;
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
    IEntrySetContextPatcher, IConfigurable<CadmusEntrySetContextPatcherOptions>
{
    private readonly StandardItemSortKeyBuilder _sortKeyBuilder;
    private CadmusEntrySetContextPatcherOptions _options = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="CadmusEntrySetContextPatcher"/>
    /// class.
    /// </summary>
    public CadmusEntrySetContextPatcher()
    {
        _sortKeyBuilder = new StandardItemSortKeyBuilder();
    }

    /// <summary>
    /// Configures this patcher with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(CadmusEntrySetContextPatcherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        ConfigureBaseOptions(options);
        _options = options;
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

        // remove empty items if not allowed
        if (!_options.AllowEmptyItems)
        {
            ctx.Items.RemoveAll(item => item.Parts.Count == 0);
        }

        // patch items
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

/// <summary>
/// Options for <see cref="CadmusEntrySetContextPatcher"/>.
/// </summary>
public class CadmusEntrySetContextPatcherOptions : DisabledOptions
{
    /// <summary>
    /// True to allow empty items in the context; otherwise, false. Default is
    /// false. When empty items are not allowed, any item without parts is
    /// removed by the patcher.
    /// </summary>
    /// <remarks>Typically when importing items from CSV an item is created
    /// for each row; but this happens also for the first empty row, which
    /// signals the end of data. In this case this would create a last empty
    /// item, which usually is unwanted.</remarks>
    public bool AllowEmptyItems { get; set; }
}
