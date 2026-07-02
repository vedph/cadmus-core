using System.Reflection;
using Xunit;

namespace Cadmus.Import.Proteus.Test;

public sealed class ThesaurusEntryMapTest
{
    private static ThesaurusEntryMap GetThesaurusEntryMap()
    {
        ThesaurusEntryMap map = new();
        map.Load(Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Cadmus.Import.Proteus.Test.Assets.Thesauri.json")!);
        return map;
    }

    [Fact]
    public void GetThesaurus_NotExistingId_Null()
    {
        ThesaurusEntryMap map = GetThesaurusEntryMap();
        Assert.Null(map.GetThesaurus("xxx@en"));
    }

    [Fact]
    public void GetThesaurus_ExistingId_NotNull()
    {
        ThesaurusEntryMap map = GetThesaurusEntryMap();
        Assert.NotNull(map.GetThesaurus("colors@en"));
    }

    [Fact]
    public void GetThesaurus_ExistingAlias_NotNull()
    {
        ThesaurusEntryMap map = GetThesaurusEntryMap();
        Assert.NotNull(map.GetThesaurus("my-colors@en"));
    }

    [Fact]
    public void GetEntryId_NotExistingThesaurus_Null()
    {
        ThesaurusEntryMap map = GetThesaurusEntryMap();

        string? id = map.GetEntryId("xxx@en", "red");
        Assert.Null(id);
    }

    [Fact]
    public void GetEntryId_NotExistingEntry_Null()
    {
        ThesaurusEntryMap map = GetThesaurusEntryMap();

        string? id = map.GetEntryId("colors@en", "violet");
        Assert.Null(id);
    }

    [Fact]
    public void GetEntryId_ExistingEntry_NotNull()
    {
        ThesaurusEntryMap map = GetThesaurusEntryMap();

        string? id = map.GetEntryId("colors@en", "red");
        Assert.Equal("r", id);
    }
}
