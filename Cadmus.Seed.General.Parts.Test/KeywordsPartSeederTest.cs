using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class KeywordsPartSeederTest
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
        Type t = typeof(KeywordsPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.keywords", attr!.Tag);
    }

    [Fact]
    public void Seed_NoOptions_Null()
    {
        KeywordsPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        Assert.Null(seeder.GetPart(_item, null, _factory));
    }

    [Fact]
    public void Seed_NoLanguages_Null()
    {
        KeywordsPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new KeywordsPartSeederOptions
        {
            Languages = Array.Empty<string>()  // invalid
        });

        Assert.Null(seeder.GetPart(_item, null, _factory));
    }

    [Fact]
    public void Seed_ValidOptions_Ok()
    {
        KeywordsPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new KeywordsPartSeederOptions
        {
            Languages = new[]
            {
                "eng",
                "ita",
                "deu"
            }
        });

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        KeywordsPart? cp = part as KeywordsPart;
        Assert.NotNull(cp);

        TestHelper.AssertPartMetadata(cp!);
        Assert.NotEmpty(cp!.Keywords);
    }
}
