using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Core.Storage;
using Cadmus.Mongo;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using MongoDB.Driver;
using System;
using System.Collections.Generic;

namespace Cadmus.Export;

/// <summary>
/// MongoDB based item IDs collector. This is a simple collector for MongoDB
/// relying on <see cref="ItemFilter"/> to filter the items to collect.
/// <para>Tag: <c>it.vedph.item-id-collector.mongo</c>.</para>
/// </summary>
/// <seealso cref="IItemIdCollector" />
[Tag("it.vedph.item-id-collector.mongo")]
public sealed class MongoItemIdCollector : IItemIdCollector,
    IConfigurable<MongoItemIdCollectorOptions>
{
    private MongoItemIdCollectorOptions? _options;
    private MongoCadmusRepository? _repository;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(MongoItemIdCollectorOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        // we do not need parts here, just items
        _repository = new(new StandardPartTypeProvider(
            new TagAttributeToTypeMap()), new StandardItemSortKeyBuilder());
        _repository.Configure(new MongoCadmusRepositoryOptions
        {
            ConnectionString = _options.ConnectionString
        });
    }

    /// <summary>
    /// Gets the items IDs.
    /// </summary>
    /// <returns>IDs.</returns>
    public IEnumerable<string> GetIds()
    {
        if (_options == null) yield break;

        ItemFilter filter = new()
        {
            PageNumber = _options.PageNumber,
            PageSize = _options.PageSize,
            Title = _options.Title,
            Description = _options.Description,
            FacetId = _options.FacetId,
            GroupId = _options.GroupId,
            Flags = _options.Flags,
            FlagMatching = _options.FlagMatching,
            UserId = _options.UserId,
            MinModified = _options.MinModified,
            MaxModified = _options.MaxModified,
        };
        DataPage<ItemInfo> page = _repository!.GetItems(filter);
        while (filter.PageNumber <= page.PageCount)
        {
            foreach (ItemInfo item in page.Items)
            {
                yield return item.Id!;
            }
            filter.PageNumber++;
            page = _repository!.GetItems(filter);
        }
    }
}

/// <summary>
/// Options for <see cref="MongoItemIdCollector"/>.
/// </summary>
public class MongoItemIdCollectorOptions
{
    /// <summary>
    /// Gets or sets the connection string.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the page number to start from.
    /// </summary>
    public int PageNumber { get; set; }

    /// <summary>
    /// Gets or sets the size of the items page.
    /// </summary>
    public int PageSize { get; set; }

    /// <summary>
    /// Gets or sets the title filter.
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// Gets or sets the description filter.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the facet ID filter.
    /// </summary>
    public string? FacetId { get; set; }

    /// <summary>
    /// Gets or sets the group ID filter.
    /// </summary>
    public string? GroupId { get; set; }

    /// <summary>
    /// Gets or sets the flags filter.
    /// </summary>
    public int? Flags { get; set; }

    /// <summary>
    /// Gets or sets the flag matching mode used when <see cref="Flags"/>
    /// is not null.
    /// </summary>
    public FlagMatching FlagMatching { get; set; }

    /// <summary>
    /// Gets or sets the user identifier filter.
    /// </summary>
    public string? UserId { get; set; }

    /// <summary>
    /// Gets or sets the minimum modified date and time filter.
    /// </summary>
    public DateTime? MinModified { get; set; }

    /// <summary>
    /// Gets or sets the maximum modified date and time filter.
    /// </summary>
    public DateTime? MaxModified { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="MongoItemIdCollectorOptions"/>
    /// class.
    /// </summary>
    public MongoItemIdCollectorOptions()
    {
        PageNumber = 1;
        PageSize = 20;
    }
}
