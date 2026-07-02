using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Cadmus.Seed.General.Parts;
using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class ChronotopesPartTest
{
    private static ChronotopesPart GetPart()
    {
        ChronotopesPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (ChronotopesPart)seeder.GetPart(item, null, null)!;
    }

    private static ChronotopesPart GetEmptyPart()
    {
        return new ChronotopesPart
        {
            ItemId = Guid.NewGuid().ToString(),
            RoleId = "some-role",
            CreatorId = "zeus",
            UserId = "another",
        };
    }

    [Fact]
    public void Part_Is_Serializable()
    {
        ChronotopesPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        ChronotopesPart part2 =
            TestHelper.DeserializePart<ChronotopesPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Chronotopes.Count, part2.Chronotopes.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        ChronotopesPart part = GetPart();
        part.Chronotopes.Clear();

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Single(pins);
        DataPin pin = pins[0];
        Assert.Equal("tot-count", pin.Name);
        TestHelper.AssertPinIds(part, pin);
        Assert.Equal("0", pin.Value);
    }

    [Fact]
    public void GetDataPins_Entries_Ok()
    {
        ChronotopesPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            part.Chronotopes.Add(new AssertedChronotope
            {
                Place = new AssertedPlace
                {
                    Value = n % 2 == 0 ? "Even" : "Odd"
                },
                Date = new AssertedHistoricalDate(
                    HistoricalDate.Parse($"{1300 + n} AD")!)
            });
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(6, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("3", pin!.Value);

        pin = pins.Find(p => p.Name == "place" && p.Value == "even");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "place" && p.Value == "odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        for (int n = 1; n <= 3; n++)
        {
            pin = pins.Find(p => p.Name == "date-value" &&
                p.Value == $"{1300 + n}");
            Assert.NotNull(pin);
            TestHelper.AssertPinIds(part, pin!);
        }
    }
}
