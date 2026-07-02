using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Mat.Bricks;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="DecoratedCountsPart"/>.
/// Tag: <c>seed.it.vedph.physical-measurements</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.physical-measurements")]
public sealed class PhysicalMeasurementsPartSeeder : PartSeederBase
{
    private static List<PhysicalMeasurement> GetMeasurements(int count, Faker f)
    {
        List<PhysicalMeasurement> measurements = [];
        for (int n = 1; n <= count; n++)
        {
            measurements.Add(new PhysicalMeasurement
            {
                Name = f.PickRandom("red", "green", "blue"),
                Value = (float)Math.Round(f.Random.Float(1, 100), 1)
            });
        }
        return measurements;
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

        PhysicalMeasurementsPart part = new Faker<PhysicalMeasurementsPart>()
           .RuleFor(p => p.Measurements, f => GetMeasurements(f.Random.Number(1, 3), f))
           .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
