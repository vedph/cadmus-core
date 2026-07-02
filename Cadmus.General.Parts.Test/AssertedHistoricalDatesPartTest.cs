using Cadmus.Core;
using Cadmus.Seed.General.Parts;
using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class AssertedHistoricalDatesPartTest
{
    private static AssertedHistoricalDatesPart GetPart()
    {
        AssertedHistoricalDatesPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (AssertedHistoricalDatesPart)seeder.GetPart(item, null, null)!;
    }

    private static AssertedHistoricalDatesPart GetEmptyPart()
    {
        return new AssertedHistoricalDatesPart
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
        AssertedHistoricalDatesPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        AssertedHistoricalDatesPart part2 =
            TestHelper.DeserializePart<AssertedHistoricalDatesPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Dates.Count, part2.Dates.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        AssertedHistoricalDatesPart part = GetPart();
        part.Dates.Clear();

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
        AssertedHistoricalDatesPart part = GetEmptyPart();

        part.Dates.Add(new() { A = new Datation { Value = 100 } });
        part.Dates.Add(new() { Tag = "reuse", A = new Datation { Value = 123 } });

        List<DataPin> pins = [.. part.GetDataPins(null)];

        Assert.Equal(4, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("2", pin!.Value);

        pin = pins.Find(p => p.Name == "date-value" && p.Value == "100");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "date-value" && p.Value == "123");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "date-reuse-value" && p.Value == "123");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
