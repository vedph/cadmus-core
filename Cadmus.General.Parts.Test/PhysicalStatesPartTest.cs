using Cadmus.Core;
using Cadmus.Mat.Bricks;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class PhysicalStatesPartTest
{
    private static PhysicalStatesPart GetPart()
    {
        PhysicalStatesPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (PhysicalStatesPart)seeder.GetPart(item, null, null)!;
    }

    private static PhysicalStatesPart GetEmptyPart()
    {
        return new PhysicalStatesPart
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
        PhysicalStatesPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        PhysicalStatesPart part2 =
            TestHelper.DeserializePart<PhysicalStatesPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.States.Count, part2.States.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        PhysicalStatesPart part = GetPart();
        part.States.Clear();

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
        PhysicalStatesPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            part.States.Add(new PhysicalState
            {
                Type = "q" + n,
            });
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(4, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("3", pin!.Value);

        pin = pins.Find(p => p.Name == "type-q1-count" && p.Value == "1");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type-q2-count" && p.Value == "1");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "type-q3-count" && p.Value == "1");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
