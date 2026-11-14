using Fusi.Tools.Configuration;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ParserTagsTest
{
    private const string PREFIX = "it.vedph.entry-region-parser.xlsx.";

    private static readonly List<Type> _parsers = [.. typeof(ImportHelper)
        .Assembly.GetTypes()
        .Where(t => !t.IsAbstract &&
            typeof(IEntryRegionParser).IsAssignableFrom(t))];

    [Fact]
    public void Parsers_HavePrefixedTag()
    {
        Assert.NotEmpty(_parsers);
        Assert.All(_parsers, t =>
        {
            TagAttribute? tag = t.GetCustomAttribute<TagAttribute>(false);
            Assert.NotNull(tag);
            Assert.StartsWith(PREFIX, tag.Tag);
        });
    }

    [Fact]
    public void Parsers_TagsAreDistinct()
    {
        List<string> tags = [.. _parsers.Select(
            t => t.GetCustomAttribute<TagAttribute>(false)!.Tag)];
        Assert.Equal(tags.Count, tags.Distinct().Count());
    }
}
