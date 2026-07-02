using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class FlagsPartSeederTest
{
    private static readonly PartSeederFactory _factory =
        TestHelper.GetFactory();
    private static readonly SeedOptions _seedOptions = _factory.GetSeedOptions();
    private static readonly IItem _item =
        _factory.GetItemSeeder().GetItem(1, "facet");

    [Fact]
    public void TypeHasTagAttribute()
    {
        Type t = typeof(FlagsPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.flags", attr!.Tag);
    }

    [Fact]
    public void Seed_Ok()
    {
        FlagsPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        seeder.Configure(
            new FlagsPartSeederOptions
            {
                Flags =
                [
                    "alpha",
                    "beta",
                    "gamma",
                    "delta"
                ]
            });
        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        FlagsPart? p = part as FlagsPart;
        Assert.NotNull(p);

        TestHelper.AssertPartMetadata(p!);

        Assert.NotEmpty(p!.Flags);
    }
}
