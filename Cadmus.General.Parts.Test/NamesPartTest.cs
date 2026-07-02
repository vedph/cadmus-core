using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class NamesPartTest
{
    private static NamesPart GetPart()
    {
        NamesPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (NamesPart)seeder.GetPart(item, null, null)!;
    }

    private static NamesPart GetEmptyPart()
    {
        return new NamesPart
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
        NamesPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        NamesPart part2 =
            TestHelper.DeserializePart<NamesPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Names.Count, part2.Names.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        NamesPart part = GetPart();
        part.Names.Clear();

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
        NamesPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            part.Names.Add(new AssertedProperName(new ProperName
            {
                Language = "eng",
                Pieces = new List<ProperNamePiece>
                {
                    new ProperNamePiece
                    {
                        Type = "name",
                        Value = "Name" + new string((char)('A' + n - 1), 1)
                    }
                }
            }));
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(4, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("3", pin!.Value);

        pin = pins.Find(p => p.Name == "name" && p.Value == "namea");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "name" && p.Value == "nameb");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "name" && p.Value == "namec");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
