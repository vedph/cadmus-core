using Cadmus.Core;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class MetadataPartTest
{
    private static MetadataPart GetPart()
    {
        MetadataPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (MetadataPart)seeder.GetPart(item, null, null)!;
    }

    private static MetadataPart GetEmptyPart()
    {
        return new MetadataPart
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
        MetadataPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        MetadataPart part2 =
            TestHelper.DeserializePart<MetadataPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);

        Assert.Equal(part.Metadata.Count, part2.Metadata.Count);
    }

    [Fact]
    public void GetDataPins_NoMetadata_Ok()
    {
        MetadataPart part = GetPart();
        part.Metadata.Clear();

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Single(pins);
        DataPin pin = pins[0];
        Assert.Equal("tot-count", pin.Name);
        TestHelper.AssertPinIds(part, pin);
        Assert.Equal("0", pin.Value);
    }

    [Fact]
    public void GetDataPins_Metadata_Ok()
    {
        MetadataPart part = GetEmptyPart();

        for (int n = 1; n <= 3; n++)
        {
            part.Metadata.Add(new Metadatum
            {
                Type = n % 2 == 0 ? "even" : "odd",
                Name = "n" + n,
                Value = "v" + n
            });
        }

        List<DataPin> pins = part.GetDataPins(null).ToList();

        Assert.Equal(4, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "tot-count");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
        Assert.Equal("3", pin!.Value);

        pin = pins.Find(p => p.Name == "n1" && p.Value == "v1");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "n2" && p.Value == "v2");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "n3" && p.Value == "v3");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
