using Cadmus.Core;
using Fusi.Tools.Configuration;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Xunit;
using Cadmus.Seed.General.Parts;
using Cadmus.Refs.Bricks;

namespace Cadmus.General.Parts.Test;

public sealed class PinLinksLayerFragmentTest
{
    private static PinLinksLayerFragment GetFragment()
    {
        var seeder = new PinLinksLayerFragmentSeeder();
        return (PinLinksLayerFragment)
            seeder.GetFragment(new Item(), "1.2", "exemplum fictum");
    }

    private static PinLinksLayerFragment GetEmptyFragment()
    {
        return new PinLinksLayerFragment
        {
            Location = "1.23",
        };
    }

    [Fact]
    public void Fragment_Has_Tag()
    {
        TagAttribute? attr = typeof(PinLinksLayerFragment).GetTypeInfo()
            .GetCustomAttribute<TagAttribute>();
        string? typeId = attr != null ? attr.Tag : GetType().FullName;
        Assert.NotNull(typeId);
        Assert.StartsWith(PartBase.FR_PREFIX, typeId);
    }

    [Fact]
    public void Fragment_Is_Serializable()
    {
        PinLinksLayerFragment fragment = GetFragment();

        string json = TestHelper.SerializeFragment(fragment);
        PinLinksLayerFragment? fragment2 =
            TestHelper.DeserializeFragment<PinLinksLayerFragment>(json);

        Assert.NotNull(fragment2);
        Assert.Equal(fragment.Location, fragment2.Location);
    }

    [Fact]
    public void GetDataPins_Ok()
    {
        PinLinksLayerFragment fragment = GetEmptyFragment();
        fragment.Links.Add(new AssertedCompositeId
        {
            Target = new PinTarget
            {
                Gid = "some-id",
                ItemId = "item-id",
                PartId = "part-id",
                Label = "The label 1.",
                PartTypeId = "part-type-id",
                RoleId = "role-id",
                // Tag = "tag",
                Name = "name",
                Value = "value",
            }
        });

        List<DataPin> pins = fragment.GetDataPins(null).ToList();

        Assert.Equal(4, pins.Count);

        // fr-tot-count
        DataPin? pin = pins.Find(p => p.Name == PartBase.FR_PREFIX + "tot-count");
        Assert.NotNull(pin);
        Assert.Equal("1", pin.Value);

        // gid
        pin = pins.Find(p => p.Name == PartBase.FR_PREFIX + "gid" &&
            p.Value == "some-id");
        Assert.NotNull(pin);

        // label
        pin = pins.Find(p => p.Name == PartBase.FR_PREFIX + "label" &&
            p.Value == "the label 1");
        Assert.NotNull(pin);

        // item-id
        pin = pins.Find(p => p.Name == PartBase.FR_PREFIX + "item-id" &&
            p.Value == "item-id");
        Assert.NotNull(pin);

        // tagged-item-id
        //pin = pins.Find(p => p.Name == PartBase.FR_PREFIX + "tagged-item-id" &&
        //    p.Value == "tag item-id");
        //Assert.NotNull(pin);
    }
}
