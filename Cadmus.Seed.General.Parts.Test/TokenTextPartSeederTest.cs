using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class TokenTextPartSeederTest
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
        Type t = typeof(TokenTextPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.token-text", attr!.Tag);
    }

    [Fact]
    public void Seed_Options_Tag()
    {
        TokenTextPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        TokenTextPart? tp = part as TokenTextPart;
        Assert.NotNull(tp);

        TestHelper.AssertPartMetadata(tp!);
        Assert.NotNull(tp!.Citation);
        Assert.NotEmpty(tp.Lines);

        for (int y = 1; y <= tp.Lines.Count; y++)
        {
            Assert.Equal(y, tp.Lines[y - 1].Y);
        }
    }
}
