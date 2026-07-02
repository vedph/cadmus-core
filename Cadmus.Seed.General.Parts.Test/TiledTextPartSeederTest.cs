using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class TiledTextPartSeederTest
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
        Type t = typeof(TiledTextPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.tiled-text", attr!.Tag);
    }

    [Fact]
    public void Seed_Options_Tag()
    {
        TiledTextPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        IPart part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        TiledTextPart? tp = part as TiledTextPart;
        Assert.NotNull(tp);

        TestHelper.AssertPartMetadata(tp!);
        Assert.NotNull(tp!.Citation);
        Assert.NotEmpty(tp.Rows);

        for (int y = 1; y <= tp.Rows.Count; y++)
        {
            Assert.Equal(y, tp.Rows[y - 1].Y);
        }
    }
}
