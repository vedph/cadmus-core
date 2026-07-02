using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class PinLinksLayerFragmentSeederTest
{
    private static readonly PartSeederFactory _factory
        = TestHelper.GetFactory();
    private static readonly SeedOptions _seedOptions
        = _factory.GetSeedOptions();
    private static readonly IItem _item =
        _factory.GetItemSeeder().GetItem(1, "facet");

    [Fact]
    public void TypeHasTagAttribute()
    {
        Type t = typeof(PinLinksLayerFragmentSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.fr.it.vedph.pin-links", attr.Tag);
    }

    [Fact]
    public void GetFragmentType_Ok()
    {
        PinLinksLayerFragmentSeeder seeder = new();
        Assert.Equal(typeof(PinLinksLayerFragment), seeder.GetFragmentType());
    }

    [Fact]
    public void Seed_Ok()
    {
        PinLinksLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        ITextLayerFragment? fragment = seeder.GetFragment(_item, "1.1", "alpha");

        Assert.NotNull(fragment);

        PinLinksLayerFragment? fr = fragment as PinLinksLayerFragment;
        Assert.NotNull(fr);

        Assert.Equal("1.1", fr.Location);
    }
}
