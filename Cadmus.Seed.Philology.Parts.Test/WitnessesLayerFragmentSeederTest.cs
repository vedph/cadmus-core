using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.Philology.Parts;
using Cadmus.Seed.Parts.Test;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.Philology.Parts.Test;

public sealed class WitnessesLayerFragmentSeederTest
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
        Type t = typeof(WitnessesLayerFragmentSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.fr.it.vedph.witnesses", attr!.Tag);
    }

    [Fact]
    public void GetFragmentType_Ok()
    {
        WitnessesLayerFragmentSeeder seeder = new();
        Assert.Equal(typeof(WitnessesLayerFragment), seeder.GetFragmentType());
    }

    [Fact]
    public void Seed_NoOptions_Null()
    {
        WitnessesLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        Assert.Null(seeder.GetFragment(_item, "1.1", "alpha"));
    }

    [Fact]
    public void Seed_NoIds_Null()
    {
        WitnessesLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new WitnessesLayerFragmentSeederOptions
        {
            Ids = Array.Empty<string>()  // invalid
        });

        Assert.Null(seeder.GetFragment(_item, "1.1", "alpha"));
    }

    [Fact]
    public void Seed_ValidOptions_Ok()
    {
        WitnessesLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        string[] ids = new[]
            {
                "alpha",
                "beta",
                "gamma"
            };
        seeder.Configure(new WitnessesLayerFragmentSeederOptions
        {
            Ids = ids
        });

        ITextLayerFragment? fragment = seeder.GetFragment(_item, "1.1", "alpha");

        Assert.NotNull(fragment);

        WitnessesLayerFragment? fr = fragment as WitnessesLayerFragment;
        Assert.NotNull(fr);

        Assert.Equal("1.1", fr!.Location);
        Assert.NotEmpty(fr.Witnesses);
        foreach (Witness witness in fr.Witnesses)
        {
            Assert.True(Array.IndexOf(ids, witness.Id) > -1);
            Assert.NotNull(witness.Citation);
            Assert.NotNull(witness.Text);
        }
    }
}
