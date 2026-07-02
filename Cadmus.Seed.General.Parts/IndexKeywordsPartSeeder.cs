using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="IndexKeywordsPart"/>.
/// Tag: <c>seed.it.vedph.index-keywords</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
/// <seealso cref="IConfigurable{IndexKeywordsPartSeederOptions}" />
[Tag("seed.it.vedph.index-keywords")]
public sealed class IndexKeywordsPartSeeder : PartSeederBase,
    IConfigurable<IndexKeywordsPartSeederOptions>
{
    private IndexKeywordsPartSeederOptions? _options;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(IndexKeywordsPartSeederOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Creates and seeds a new part.
    /// </summary>
    /// <param name="item">The item this part should belong to.</param>
    /// <param name="roleId">The optional part role ID.</param>
    /// <param name="factory">The part seeder factory. This is used
    /// for layer parts, which need to seed a set of fragments.</param>
    /// <returns>A new part.</returns>
    /// <exception cref="ArgumentNullException">item or factory</exception>
    public override IPart? GetPart(IItem item, string? roleId,
        PartSeederFactory? factory)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (_options?.Languages == null || _options.Languages.Count == 0)
            return null;

        IndexKeywordsPart part = new();
        SetPartMetadata(part, roleId, item);

        int count = Randomizer.Seed.Next(1, 4);
        bool hasIndexIds = _options.IndexIds?.Count > 0;
        while (count > 0)
        {
            IndexKeyword keyword = new Faker<IndexKeyword>()
                .RuleFor(k => k.IndexId, f => hasIndexIds?
                    f.PickRandom(_options.IndexIds) : null)
                .RuleFor(k => k.Language, f => f.PickRandom(_options.Languages))
                .RuleFor(k => k.Value, f => f.Lorem.Word())
                .Generate();

            part.AddKeyword(keyword);
            count--;
        }

        return part;
    }
}

/// <summary>
/// Options for <see cref="KeywordsPartSeeder"/>.
/// </summary>
public sealed class IndexKeywordsPartSeederOptions
{
    /// <summary>
    /// Gets or sets the index IDs to pick from. You can use an empty
    /// string for the default index.
    /// </summary>
    public IList<string>? IndexIds { get; set; }

    /// <summary>
    /// Gets or sets the languages codes to pick from.
    /// </summary>
    public IList<string>? Languages { get; set; }
}
