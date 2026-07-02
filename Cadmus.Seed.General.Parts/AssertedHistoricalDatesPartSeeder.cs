using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Refs.Bricks;
using Fusi.Antiquity.Chronology;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Seed.General.Parts;

[Tag("seed.it.vedph.asserted-historical-dates")]
public sealed class AssertedHistoricalDatesPartSeeder : PartSeederBase
{
    private static AssertedHistoricalDate GetRandomDate()
    {
        AssertedHistoricalDate date = new();

        if (Randomizer.Seed.Next(1, 10) == 0)
        {
            date.SetStartPoint(HistoricalDatePartSeeder.GetRandomDatation());
            Datation b = date.A.Clone();
            b.Value++;
            date.SetEndPoint(b);
        }
        else
        {
            date.SetSinglePoint(HistoricalDatePartSeeder.GetRandomDatation());
        }

        date.Assertion = SeedHelper.GetAssertion(new Faker());

        return date;
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

        AssertedHistoricalDatesPart part = new();
        SetPartMetadata(part, roleId, item);

        int n = Randomizer.Seed.Next(1, 3);
        for (int i = 0; i < n; i++)
            part.Dates.Add(GetRandomDate());

        return part;
    }
}
