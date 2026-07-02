using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class CommentPartSeederTest
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
        Type t = typeof(CommentPartSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.it.vedph.comment", attr!.Tag);
    }

    [Fact]
    public void Seed_ValidOptions_Ok()
    {
        CommentPartSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new CommentPartSeederOptions());

        IPart? part = seeder.GetPart(_item, null, _factory);

        Assert.NotNull(part);

        CommentPart? cp = part as CommentPart;
        Assert.NotNull(cp);

        TestHelper.AssertPartMetadata(cp!);
        Assert.NotEmpty(cp!.References);
        Assert.NotEmpty(cp!.Links);
        Assert.NotEmpty(cp!.Categories);
        Assert.NotEmpty(cp!.Keywords);
    }
}
