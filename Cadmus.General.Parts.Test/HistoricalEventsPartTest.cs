using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Cadmus.Seed.General.Parts;
using Fusi.Antiquity.Chronology;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class HistoricalEventsPartTest
{
    private static HistoricalEventsPart GetPart()
    {
        HistoricalEventsPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (HistoricalEventsPart)seeder.GetPart(item, null, null);
    }

    private static HistoricalEventsPart GetEmptyPart()
    {
        return new HistoricalEventsPart
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
        HistoricalEventsPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        HistoricalEventsPart part2 =
            TestHelper.DeserializePart<HistoricalEventsPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Events.Count, part2.Events.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        HistoricalEventsPart part = GetPart();
        part.Events.Clear();

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Single(pins);
        DataPin pin = pins[0];
        Assert.Equal("tot-count", pin.Name);
        TestHelper.AssertPinIds(part, pin);
        Assert.Equal("0", pin.Value);
    }

    [Fact]
    public void GetDataPins_Ok()
    {
        HistoricalEventsPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            bool even = n % 2 == 0;
            part.Events.Add(new HistoricalEvent
            {
                Eid = "n" + n,
                Type = even ? "even" : "odd",
                Chronotopes = new List<AssertedChronotope>{
                    new AssertedChronotope
                    {
                        Place = new AssertedPlace
                        {
                            Value = even ? "Even" : "Odd"
                        },
                        Date = new AssertedHistoricalDate(
                            HistoricalDate.Parse($"{1300 + n} AD")!)
                    }
                },
                RelatedEntities = even
                    ? [
                        new()
                        {
                            Relation = "evenness",
                            Id = new AssertedCompositeId
                            {
                                Target = new PinTarget
                                {
                                    Gid = $"e{n}",
                                    Label = $"e{n}"
                                },
                            }
                        }
                    ]
                    : []
            });
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(25, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("3", pin!.Value);

        // n1 n3 odd Odd 1301 1303
        pin = pins.Find(p => p.Name == "eid" && p.Value == "n1");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "eid" && p.Value == "n3");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type" && p.Value == "odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "place" && p.Value == "Odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "date-value" && p.Value == "1301");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "date-value" && p.Value == "1303");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasPlace@n1" && p.Value == "Odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasPlace@n3" && p.Value == "Odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasDate@n1" && p.Value == "1301 AD");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasDate@n3" && p.Value == "1303 AD");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasDateValue@n1" && p.Value == "1301");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasDateValue@n3" && p.Value == "1303");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type@n1" && p.Value == "odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type@n3" && p.Value == "odd");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        // n2 even Even 1302 e2
        pin = pins.Find(p => p.Name == "eid" && p.Value == "n2");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type" && p.Value == "even");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "place" && p.Value == "Even");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "date-value" && p.Value == "1302");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasPlace@n2" && p.Value == "Even");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasDate@n2" && p.Value == "1302 AD");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "hasDateValue@n2" && p.Value == "1302");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type@n2" && p.Value == "even");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "eid2@n2" && p.Value == "e2");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "rel@n2@e2" && p.Value == "evenness");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
