using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.Philology.Parts;
using Cadmus.Seed.Parts.Test;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.Philology.Parts.Test;

public sealed class OrthographyLayerFragmentSeederTest
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
        Type t = typeof(OrthographyLayerFragmentSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.fr.it.vedph.orthography", attr!.Tag);
    }

    [Fact]
    public void GetFragmentType_Ok()
    {
        OrthographyLayerFragmentSeeder seeder = new();
        Assert.Equal(typeof(OrthographyLayerFragment), seeder.GetFragmentType());
    }

    [Fact]
    public void Seed_WithoutTags_Ok()
    {
        OrthographyLayerFragmentSeeder seeder = new();

        ITextLayerFragment? fragment = seeder.GetFragment(_item, "1.1", "alpha");

        Assert.NotNull(fragment);

        OrthographyLayerFragment? fr = fragment as OrthographyLayerFragment;
        Assert.NotNull(fr);

        Assert.Equal("1.1", fr!.Location);
        Assert.NotNull(fr.Reference);
        Assert.Single(fr.Operations);
        MspOperation? op = MspOperation.Parse(fr.Operations[0]);
        Assert.Null(op!.Tag);
    }

    [Fact]
    public void Seed_WithTags_Ok()
    {
        OrthographyLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new OrthographyLayerFragmentSeederOptions
        {
            Tags = [ "A", "B", "C" ]
        });

        ITextLayerFragment? fragment = seeder.GetFragment(_item, "1.1", "alpha");

        Assert.NotNull(fragment);

        OrthographyLayerFragment? fr = fragment as OrthographyLayerFragment;
        Assert.NotNull(fr);

        Assert.Equal("1.1", fr!.Location);
        Assert.NotNull(fr.Reference);
        Assert.Single(fr.Operations);
        MspOperation? op = MspOperation.Parse(fr.Operations[0]);
        Assert.NotNull(op!.Tag);
    }
}
