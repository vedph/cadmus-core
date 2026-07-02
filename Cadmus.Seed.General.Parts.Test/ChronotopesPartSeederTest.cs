using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class ChronotopesPartSeederTest
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
        Type t = typeof(ChronotopesPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.chronotopes", attr!.Tag);
    }

    [Fact]
    public void Seed_Ok()
    {
        ChronotopesPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        ChronotopesPart? p = part as ChronotopesPart;
        Assert.NotNull(p);

        TestHelper.AssertPartMetadata(p!);

        Assert.NotEmpty(p!.Chronotopes);
    }
}
