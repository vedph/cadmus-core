using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="KeywordsPart"/>.
/// Tag: <c>seed.it.vedph.keywords</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
/// <seealso cref="IConfigurable{KeywordsPartSeederOptions}" />
[Tag("seed.it.vedph.keywords")]
public sealed class KeywordsPartSeeder : PartSeederBase,
    IConfigurable<KeywordsPartSeederOptions>
{
    private KeywordsPartSeederOptions? _options;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(KeywordsPartSeederOptions options)
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
        ArgumentNullException.ThrowIfNull(factory);

        if (_options?.Languages == null || _options.Languages.Count == 0)
            return null;

        KeywordsPart part = new();
        SetPartMetadata(part, roleId, item);

        int count = Randomizer.Seed.Next(1, 4);
        while (count > 0)
        {
            Keyword keyword = new Faker<Keyword>()
                .RuleFor(k => k.Language, f => f.PickRandom(_options.Languages))
                .RuleFor(k => k.Value, f => f.Lorem.Word())
                .Generate();

            part.AddKeyword(keyword.Language!, keyword.Value!);
            count--;
        }

        return part;
    }
}

/// <summary>
/// Options for <see cref="KeywordsPartSeeder"/>.
/// </summary>
public sealed class KeywordsPartSeederOptions
{
    /// <summary>
    /// Gets or sets the languages codes to pick from.
    /// </summary>
    public IList<string>? Languages { get; set; }
}
