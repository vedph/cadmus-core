using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class CategoriesPartSeederTest
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
        Type t = typeof(CategoriesPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.categories", attr!.Tag);
    }

    [Fact]
    public void Seed_NoOptions_Null()
    {
        CategoriesPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        Assert.Null(seeder.GetPart(_item, null, _factory));
    }

    [Fact]
    public void Seed_InvalidMax_Null()
    {
        CategoriesPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new CategoriesPartSeederOptions
        {
            MaxCategoriesPerItem = 0,   // invalid
            Categories =
            [
                "alpha",
                "beta",
                "gamma"
            ]
        });

        Assert.Null(seeder.GetPart(_item, null, _factory));
    }

    [Fact]
    public void Seed_NoCategories_Null()
    {
        CategoriesPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new CategoriesPartSeederOptions
        {
            MaxCategoriesPerItem = 3,
            Categories = Array.Empty<string>()  // invalid
        });

        Assert.Null(seeder.GetPart(_item, null, _factory));
    }

    [Fact]
    public void Seed_ValidOptions_Ok()
    {
        CategoriesPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new CategoriesPartSeederOptions
        {
            MaxCategoriesPerItem = 3,
            Categories =
            [
                "alpha",
                "beta",
                "gamma"
            ]
        });

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        CategoriesPart? cp = part as CategoriesPart;
        Assert.NotNull(cp);

        TestHelper.AssertPartMetadata(cp!);
        Assert.NotEmpty(cp!.Categories);
    }
}
