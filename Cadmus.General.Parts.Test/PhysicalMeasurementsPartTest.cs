using Cadmus.Core;
using Cadmus.Mat.Bricks;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class PhysicalMeasurementsPartTest
{
    private static PhysicalMeasurementsPart GetPart()
    {
        PhysicalMeasurementsPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (PhysicalMeasurementsPart)seeder.GetPart(item, null, null)!;
    }

    private static PhysicalMeasurementsPart GetEmptyPart()
    {
        return new PhysicalMeasurementsPart
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
        PhysicalMeasurementsPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        PhysicalMeasurementsPart part2 =
            TestHelper.DeserializePart<PhysicalMeasurementsPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Measurements.Count, part2.Measurements.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        PhysicalMeasurementsPart part = GetPart();
        part.Measurements.Clear();

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
        PhysicalMeasurementsPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            bool odd = n % 2 == 1;
            part.Measurements.Add(new PhysicalMeasurement
            {
                Name = odd? "odd" : "even",
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
