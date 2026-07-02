using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class TiledTextLayerPartSeederTest
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
        Type t = typeof(TiledTextLayerPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.tiled-text-layer", attr!.Tag);
    }

    [Fact]
    public void Seed_NoOptions_Null()
    {
        TiledTextLayerPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.Null(part);
    }

    [Fact]
    public void Seed_InvalidOptions_Null()
    {
        TiledTextLayerPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new TiledTextLayerPartSeederOptions
        {
            MaxFragmentCount = 0
        });

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.Null(part);
    }

    [Fact]
    public void Seed_OptionsNoText_Null()
    {
        TiledTextLayerPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new TiledTextLayerPartSeederOptions
        {
            MaxFragmentCount = 3
        });

        IPart? part = seeder.GetPart(_item, "fr.it.vedph.comment", _factory);

        Assert.Null(part);
    }

    [Fact]
    public void Seed_Options_Ok()
    {
        TiledTextLayerPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new TiledTextLayerPartSeederOptions
        {
            MaxFragmentCount = 3
        });

        // item with text
        IItem item = _factory.GetItemSeeder().GetItem(1, "facet");
        TiledTextPartSeeder textSeeder = new();
        textSeeder.SetSeedOptions(_seedOptions);
        item.Parts.Add(textSeeder.GetPart(_item, null, _factory));

        IPart? part = seeder.GetPart(item, "fr.it.vedph.comment", _factory);

        Assert.NotNull(part);

        TiledTextLayerPart<CommentLayerFragment>? lp =
            part as TiledTextLayerPart<CommentLayerFragment>;
        Assert.NotNull(lp);
        Assert.NotEmpty(lp!.Fragments);
    }
}
