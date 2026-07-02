using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class ChronologyLayerFragmentSeederTest
{
    private static readonly PartSeederFactory _factory =
            TestHelper.GetFactory();
    private static readonly SeedOptions _seedOptions =
        _factory.GetSeedOptions();
    private static readonly IItem _item =
        _factory.GetItemSeeder().GetItem(1, "facet");

    [Fact]
    public void TypeHasTagAttribute()
    {
        Type t = typeof(ChronologyLayerFragmentSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.fr.it.vedph.chronology", attr!.Tag);
    }

    [Fact]
    public void GetFragmentType_Ok()
    {
        ChronologyLayerFragmentSeeder seeder = new();
        Assert.Equal(typeof(ChronologyLayerFragment), seeder.GetFragmentType());
    }

    [Fact]
    public void Seed_WithOptions_Ok()
    {
        ChronologyLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new ChronologyLayerFragmentSeederOptions
        {
            Tags = new[]
            {
                "battle",
                "priesthood",
                "consulship"
            }
        });

        ITextLayerFragment? fragment = seeder.GetFragment(_item, "1.1", "alpha");

        Assert.NotNull(fragment);

        ChronologyLayerFragment? fr = fragment as ChronologyLayerFragment;
        Assert.NotNull(fr);

        Assert.Equal("1.1", fr!.Location);
        Assert.NotNull(fr.Label);
        Assert.False(fr.Date!.A.IsUndefined());
        Assert.NotNull(fr.EventId);
        Assert.NotNull(fr.Tag);
    }
}
