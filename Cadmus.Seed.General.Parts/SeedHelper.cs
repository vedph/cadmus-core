using Bogus;
using Cadmus.Refs.Bricks;
using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

internal static class SeedHelper
{
    /// <summary>
    /// Gets a random number of document references.
    /// </summary>
    /// <param name="min">The min number of references to get.</param>
    /// <param name="max">The max number of references to get.</param>
    /// <returns>References.</returns>
    public static List<DocReference> GetDocReferences(int min, int max)
    {
        List<DocReference> refs = [];

        for (int n = 1; n <= Randomizer.Seed.Next(min, max + 1); n++)
        {
            refs.Add(new Faker<DocReference>()
                .RuleFor(r => r.Type, "book")
                .RuleFor(r => r.Tag, f => f.PickRandom(null, "tag"))
                .RuleFor(r => r.Type, "biblio")
                .RuleFor(r => r.Citation,
                    f => f.Person.LastName + " " + f.Date.Past(10).Year)
                .RuleFor(r => r.Note, f => f.Lorem.Sentence())
                .Generate());
        }

        return refs;
    }

    /// <summary>
    /// Gets a list of asserted IDs.
    /// </summary>
    /// <param name="min">The min number of IDs to get.</param>
    /// <param name="max">The max number of IDs to get.</param>
    /// <returns>IDs.</returns>
    public static List<AssertedId> GetAssertedIds(int min, int max)
    {
        List<AssertedId> ids = [];
        Faker faker = new();

        for (int n = 1; n <= Randomizer.Seed.Next(min, max + 1); n++)
        {
            ids.Add(faker.Random.Bool()
                ? new AssertedId
                {
                    Value = faker.Internet.Url(),
                    Scope = "myweb"
                } : new AssertedId
                {
                    Value = faker.Hacker.Abbreviation(),
                    Scope = "mydb"
                });
        }

        return ids;
    }

    /// <summary>
    /// Gets a list of asserted composite IDs.
    /// </summary>
    /// <param name="min">The min number of IDs to get.</param>
    /// <param name="max">The max number of IDs to get.</param>
    /// <returns>IDs.</returns>
    public static List<AssertedCompositeId> GetAssertedCompositeIds(
        int min, int max)
    {
        List<AssertedCompositeId> ids = [];

        for (int n = 1; n <= Randomizer.Seed.Next(min, max + 1); n++)
        {
            ids.Add(new AssertedCompositeId
            {
                Target = new PinTarget
                {
                    Gid = $"http://www.resources.org/n{n}",
                    Label = $"res:n{n}"
                }
            });
        }

        return ids;
    }

    /// <summary>
    /// Gets a list of asserted chronotopes.
    /// </summary>
    /// <param name="count">The count.</param>
    /// <returns>Chronotopes.</returns>
    public static List<AssertedChronotope> GetAssertedChronotopes(int count)
    {
        List<AssertedChronotope> chronotopes = [];
        for (int n = 1; n <= count; n++)
        {
            bool even = n % 2 == 0;
            chronotopes.Add(new AssertedChronotope
            {
                Place = new AssertedPlace
                {
                    Value = even ? "Even" : "Odd"
                },
                Date = new AssertedHistoricalDate(
                    HistoricalDate.Parse($"{1300 + n} AD")!)
            });
        }
        return chronotopes;
    }

    /// <summary>
    /// Gets a random assertion.
    /// </summary>
    /// <param name="faker">The faker to use.</param>
    /// <returns>Assertion.</returns>
    public static Assertion GetAssertion(Faker faker)
    {
        ArgumentNullException.ThrowIfNull(faker);

        return new Assertion
        {
            Tag = faker.Random.Bool(0.25F)? faker.Random.Word() : null,
            Rank = (short)faker.Random.Number(1, 3),
            References = GetDocReferences(1, 3),
            Note = faker.Random.Bool(0.25f) ? faker.Lorem.Sentence() : null
        };
    }
}
