using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="MetadataPart"/>.
/// Tag: <c>seed.it.vedph.metadata</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.metadata")]
public sealed class MetadataPartSeeder : PartSeederBase
{
    private static readonly string[] DataTypes = new[]
    {
        "xs:boolean",
        "xs:integer",
        "xs:string"
    };

    private static List<Metadatum> GetMetadata(int count)
    {
        List<Metadatum> metadata = new();
        for (int n = 1; n <= count; n++)
        {
            int type = Randomizer.Seed.Next(0, DataTypes.Length);
            switch (type)
            {
                case 1:     // integer
                    bool year = Randomizer.Seed.Next(0, 2) == 1;
                    metadata.Add(new Faker<Metadatum>()
                        .RuleFor(m => m.Type, DataTypes[type])
                        .RuleFor(m => m.Name, year? "date" : "consistency")
                        .RuleFor(m => m.Value, f => year
                            ? f.Random.Number(1900, DateTime.Now.Year)
                                .ToString(CultureInfo.InvariantCulture)
                            : f.Random.Number(1, 100)
                                .ToString(CultureInfo.InvariantCulture))
                        .Generate());
                    break;
                case 2:     // string
                    metadata.Add(new Faker<Metadatum>()
                        .RuleFor(m => m.Type, DataTypes[type])
                        .RuleFor(m => m.Name, f => f.Lorem.Word())
                        .RuleFor(m => m.Value, f => f.Lorem.Word())
                        .Generate());
                    break;
                default:    // boolean
                    metadata.Add(new Faker<Metadatum>()
                        .RuleFor(m => m.Type, DataTypes[type])
                        .RuleFor(m => m.Name, f => f.PickRandom("lost", "fragmentary"))
                        .RuleFor(m => m.Value, f => f.Random.Bool() ? "1" : "0")
                        .Generate());
                    break;
            }
        }
        return metadata;
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

        MetadataPart part = new Faker<MetadataPart>()
           .RuleFor(p => p.Metadata, f => GetMetadata(f.Random.Number(1, 3)))
           .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
