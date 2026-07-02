using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="ChronotopesPart"/> part.
/// Tag: <c>seed.it.vedph.chronotopes</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.chronotopes")]
public sealed class ChronotopesPartSeeder : PartSeederBase
{
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

        ChronotopesPart part = new Faker<ChronotopesPart>()
           .RuleFor(p => p.Chronotopes,
                f => SeedHelper.GetAssertedChronotopes(f.Random.Number(1,3)))
           .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
