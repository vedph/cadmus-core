using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Reflection;
using Xunit;

namespace Cadmus.Seed.General.Parts.Test;

public sealed class CommentLayerFragmentSeederTest
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
        Type t = typeof(CommentLayerFragmentSeeder);
        TagAttribute? attr = t.GetTypeInfo().GetCustomAttribute<TagAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("seed.fr.it.vedph.comment", attr!.Tag);
    }

    [Fact]
    public void GetFragmentType_Ok()
    {
        CommentLayerFragmentSeeder seeder = new();
        Assert.Equal(typeof(CommentLayerFragment), seeder.GetFragmentType());
    }

    [Fact]
    public void Seed_Ok()
    {
        CommentLayerFragmentSeeder seeder = new();
        seeder.SetSeedOptions(_seedOptions);
        seeder.Configure(new CommentPartSeederOptions());

        ITextLayerFragment? fragment = seeder.GetFragment(_item, "1.1", "alpha");

        Assert.NotNull(fragment);

        CommentLayerFragment? fr = fragment as CommentLayerFragment;
        Assert.NotNull(fr);

        Assert.Equal("1.1", fr!.Location);
        Assert.NotNull(fr.Tag);
        Assert.NotNull(fr.Text);
        Assert.NotEmpty(fr.References);
        Assert.NotEmpty(fr.Links);
        Assert.NotEmpty(fr.Categories);
        Assert.NotEmpty(fr.Keywords);
    }
}
