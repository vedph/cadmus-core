using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Mat.Bricks;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="PhysicalStatesPart"/>.
/// Tag: <c>seed.it.vedph.physical-states</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.physical-states")]
public sealed class PhysicalStatesPartSeeder : PartSeederBase
{
    private static List<PhysicalState> GetPhysicalStates(int count, Faker f)
    {
        List<PhysicalState> states = [];
        string[] types = ["q1", "q2", "q3"];

        for (int n = 1; n <= count; n++)
        {
            states.Add(new PhysicalState
            {
                Type = f.PickRandom(types),
                Reporter = f.Name.FullName(),
                Date = f.Date.PastDateOnly(5),
                Note = f.Random.Bool(0.3f) ? f.Lorem.Sentence() : null
            });
        }

        return states;
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

        PhysicalStatesPart part = new Faker<PhysicalStatesPart>()
           .RuleFor(p => p.States, f => GetPhysicalStates(f.Random.Number(1, 3), f))
           .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
