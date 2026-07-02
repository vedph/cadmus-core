using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="GrfSummaryPart"/>.
/// Tag: <c>seed.it.vedph.district-location</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.district-location")]
public sealed class DistrictLocationPartSeeder : PartSeederBase
{
    static private ProperName GetPlace(Faker faker)
    {
        ProperName name = new()
        {
            Language = "ita",
        };
        name!.Pieces!.Add(new ProperNamePiece
        {
            Type = "sestriere",
            Value = faker.PickRandom("dd", "gd")
        });
        name!.Pieces!.Add(new ProperNamePiece
        {
            Type = "location",
            Value = faker.Address.StreetName() + ", " + faker.Random.Number(1, 50)
        });
        return name;
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

        DistrictLocationPart part = new Faker<DistrictLocationPart>()
           .RuleFor(p => p.Place, GetPlace)
           .RuleFor(p => p.Note, f => f.Random.Bool(0.25f)
                         ? f.Lorem.Sentence() : null)
           .Generate();

        SetPartMetadata(part, roleId, item);

        return part;
    }
}
