using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class DecoratedCountsPartTest
{
    private static DecoratedCountsPart GetPart()
    {
        DecoratedCountsPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (DecoratedCountsPart)seeder.GetPart(item, null, null)!;
    }

    private static DecoratedCountsPart GetEmptyPart()
    {
        return new DecoratedCountsPart
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
        DecoratedCountsPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        DecoratedCountsPart part2 =
            TestHelper.DeserializePart<DecoratedCountsPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Counts.Count, part2.Counts.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        DecoratedCountsPart part = GetPart();
        part.Counts.Clear();

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
        DecoratedCountsPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            bool odd = n % 2 == 1;
            part.Counts.Add(new DecoratedCount
            {
                Id = odd ? "odd" : "even",
                Value = n * 10
            });
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(5, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("3", pin!.Value);

        pin = pins.Find(p => p.Name == "odd-min" && p.Value == "10");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "odd-max" && p.Value == "30");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "even-min" && p.Value == "20");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "even-max" && p.Value == "20");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
