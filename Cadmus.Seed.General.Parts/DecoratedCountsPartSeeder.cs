using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="DecoratedCountsPart"/>.
/// Tag: <c>seed.it.vedph.decorated-counts</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.decorated-counts")]
public sealed class DecoratedCountsPartSeeder : PartSeederBase
{
    private static List<DecoratedCount> GetCounts(int count, Faker f)
    {
        List<DecoratedCount> counts = [];
        for (int n = 1; n <= count; n++)
        {
            counts.Add(new DecoratedCount
            {
                Id = f.PickRandom("red", "green", "blue"),
                Value = f.Random.Int(1, 100)
            });
        }
        return counts;
    }

    /// <summary>
    /// Creates and seeds a new part.
    /// </summary>
    /// <param name="item">The item this part should belong to.</param>
    /// <param name="roleId">The optional part role ID.</param>
    /// <param name="factory">The part seeder factory. This is used
    /// for layer parts, which need to seed a set of fragments.</param>
    /// <returns>A new part or null.</returns>
    /// <exception cref="ArgumentNullException">item or factory</exception>
    public override IPart? GetPart(IItem item, string? roleId,
        PartSeederFactory? factory)
    {
        ArgumentNullException.ThrowIfNull(item);

        DecoratedCountsPart part = new Faker<DecoratedCountsPart>()
           .RuleFor(p => p.Counts, f => GetCounts(f.Random.Number(1, 3), f))
           .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
