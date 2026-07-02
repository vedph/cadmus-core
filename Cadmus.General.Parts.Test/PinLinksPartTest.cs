using Cadmus.Core;
using Cadmus.Refs.Bricks;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class PinLinksPartTest
{
    private static PinLinksPart GetPart()
    {
        PinLinksPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (PinLinksPart)seeder.GetPart(item, null, null)!;
    }

    private static PinLinksPart GetEmptyPart()
    {
        return new PinLinksPart
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
        PinLinksPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        PinLinksPart part2 =
            TestHelper.DeserializePart<PinLinksPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Links.Count, part2.Links.Count);
    }

    [Fact]
    public void GetDataPins_NoEntries_Ok()
    {
        PinLinksPart part = GetPart();
        part.Links.Clear();

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
        PinLinksPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            part.Links.Add(new AssertedCompositeId
            {
                Target = new PinTarget
                {
                    Gid = $"x{n}",
                    Label = $"Link #{n}.",
                    ItemId = $"i{n}",
                    PartId = $"p{n}",
                    RoleId = $"r{n}",
                    Name = "a",
                    Value = "eid"
                }
            });
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(1 + 3 * 3, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin);
        Assert.Equal("3", pin!.Value);

        for (int n = 1; n <= 3; n++)
        {
            // gid
            pin = pins.Find(p => p.Name == "gid" &&
                p.Value == $"x{n}");
            Assert.NotNull(pin);
            TestHelper.AssertPinIds(part, pin);

            // label
            pin = pins.Find(p => p.Name == "label" &&
                p.Value == $"link {n}");
            Assert.NotNull(pin);
            TestHelper.AssertPinIds(part, pin);

            // item-id
            pin = pins.Find(p => p.Name == "item-id" && p.Value == $"i{n}");
            Assert.NotNull(pin);
            TestHelper.AssertPinIds(part, pin!);
        }
    }
}
