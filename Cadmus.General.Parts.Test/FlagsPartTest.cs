using Cadmus.Core;
using Cadmus.Seed.General.Parts;
using System;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.General.Parts.Test;

public sealed class FlagsPartTest
{
    private static FlagsPart GetPart()
    {
        FlagsPartSeeder seeder = new();
        seeder.Configure(new FlagsPartSeederOptions
        {
            Flags =
            [
                "alpha", "beta", "gamma", "delta", "epsilon"
            ]
        });
        IItem item = new Item
        {
            FacetId = "default",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "Test item",
            Title = "Test Item",
            SortKey = ""
        };
        return (FlagsPart)seeder.GetPart(item, null, null)!;
    }

    private static FlagsPart GetEmptyPart()
    {
        return new FlagsPart
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
        FlagsPart part = GetPart();

        string json = TestHelper.SerializePart(part);
        FlagsPart part2 = TestHelper.DeserializePart<FlagsPart>(json)!;

        Assert.Equal(part.Id, part2.Id);
        Assert.Equal(part.TypeId, part2.TypeId);
        Assert.Equal(part.ItemId, part2.ItemId);
        Assert.Equal(part.RoleId, part2.RoleId);
        Assert.Equal(part.CreatorId, part2.CreatorId);
        Assert.Equal(part.UserId, part2.UserId);
    }

    [Fact]
    public void GetDataPins_NoFlags_Empty()
    {
        FlagsPart part = GetEmptyPart();

        Assert.Empty(part.GetDataPins());
    }

    [Fact]
    public void GetDataPins_Flags_NotEmpty()
    {
        FlagsPart part = GetEmptyPart();
        part.Flags = ["alpha", "gamma"];

        List<DataPin> pins = [.. part.GetDataPins(null)];
        Assert.Equal(2, pins.Count);

        DataPin? pin = pins.Find(p => p.Name == "flag" && p.Value == "alpha");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);

        pin = pins.Find(p => p.Name == "flag" && p.Value == "gamma");
        Assert.NotNull(pin);
        TestHelper.AssertPinIds(part, pin!);
    }
}
