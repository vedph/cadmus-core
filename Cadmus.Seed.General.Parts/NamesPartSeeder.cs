using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="NamesPart"/>.
/// Tag: <c>seed.it.vedph.names</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.names")]
public sealed class NamesPartSeeder : PartSeederBase
{
    private static List<AssertedProperName> GetNames(int count)
    {
        List<AssertedProperName> names = new();
        for (int n = 1; n <= count; n++)
        {
            names.Add(new Faker<AssertedProperName>()
                .RuleFor(a => a.Language, "eng")
                .RuleFor(a => a.Pieces, f => new List<ProperNamePiece>
                    {
                        new ProperNamePiece
                        {
                            Type = "first",
                            Value = f.Name.FirstName()
                        },
                        new ProperNamePiece
                        {
                            Type = "last",
                            Value = f.Name.LastName()
                        },
                    }
                ).Generate());
        }
        return names;
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

        NamesPart part = new Faker<NamesPart>()
           .RuleFor(p => p.Names, f => GetNames(f.Random.Number(1, 3)))
           .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
