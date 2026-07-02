using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Antiquity.Chronology;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="HistoricalDatePart"/>.
/// Tag: <c>seed.it.vedph.historical-date</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.historical-date")]
public sealed class HistoricalDatePartSeeder : PartSeederBase
{
    internal static Datation GetRandomDatation()
    {
        if (Randomizer.Seed.Next(0, 5) == 0)
        {
            return new Datation
            {
                Value = Randomizer.Seed.Next(-8, 6),
                IsCentury = true,
                IsApproximate = Randomizer.Seed.Next(0, 10) == 0
            };
        }

        int year = Randomizer.Seed.Next(-753, 477);
        short day = 0, month = 0;
        if (Randomizer.Seed.Next(0, 10) == 0)
        {
            day = (short)Randomizer.Seed.Next(1, 29);
            month = (short)Randomizer.Seed.Next(1, 13);
        }
        return new Datation
        {
            Value = year,
            Day = day,
            Month = month
        };
    }

    private static HistoricalDate GetRandomDate()
    {
        HistoricalDate date = new();

        if (Randomizer.Seed.Next(1, 10) == 0)
        {
            date.SetStartPoint(GetRandomDatation());
            Datation b = date.A.Clone();
            b.Value++;
            date.SetEndPoint(b);
        }
        else
        {
            date.SetSinglePoint(GetRandomDatation());
        }
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

        HistoricalDatePart part = new();
        SetPartMetadata(part, roleId, item);

        part.Date = GetRandomDate();
        if (Randomizer.Seed.Next(2) == 1)
            part.References = SeedHelper.GetDocReferences(1, 3);

        return part;
    }
}
