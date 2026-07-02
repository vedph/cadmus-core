using System;
using Xunit;
using Cadmus.Core;
using System.Collections.Generic;
using System.Linq;
using Cadmus.Refs.Bricks;
using Cadmus.Seed.General.Parts;

namespace Cadmus.General.Parts.Test;

public sealed class DistrictLocationPartTest
{
    private static DistrictLocationPart GetPart()
    {
        DistrictLocationPartSeeder seeder = new();
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (DistrictLocationPart)seeder.GetPart(item, null, null)!;
    }

    private static DistrictLocationPart GetEmptyPart()
    {
        return new DistrictLocationPart
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
        DistrictLocationPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        DistrictLocationPart part2 =
            TestHelper.DeserializePart<DistrictLocationPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);
    }

    [Fact]
    public void GetDataPins_Ok()
    {
        DistrictLocationPart part = GetEmptyPart();
        part.Place = new ProperName
        {
            Language = "ita",
        };
        part.Place.Pieces!.Add(new ProperNamePiece
        {
            Type = "sestriere",
            Value = "Dorsoduro"
        });

        List<DataPin> pins = part.GetDataPins(null).ToList();
        Assert.Single(pins);

        // place
        DataPin? pin = pins.Find(p => p.Name == "place" && p.Value == "dorsoduro");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
