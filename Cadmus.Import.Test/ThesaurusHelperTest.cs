using Cadmus.Core.Config;
using Xunit;

namespace Cadmus.Import.Test;

public sealed class ThesaurusHelperTest
{
    private static Thesaurus GetRgbThesaurus()
    {
        Thesaurus t = new()
        {
            Id = "colors@en"
        };
        t.AddEntry(new ThesaurusEntry { Id = "r", Value = "red" });
        t.AddEntry(new ThesaurusEntry { Id = "g", Value = "green" });
        t.AddEntry(new ThesaurusEntry { Id = "b", Value = "blue" });
        return t;
    }

    #region Replace
    [Fact]
    public void CopyThesaurus_Replace_NoAliasVsNoAlias_Replaced()
    {
        Thesaurus source = new()
        {
            Id = "colors@en"
        };
        source.AddEntry(new ThesaurusEntry { Id = "r", Value = "rosso" });
        source.AddEntry(new ThesaurusEntry { Id = "g", Value = "verde" });

        Thesaurus target = GetRgbThesaurus();

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Replace);

        Assert.NotNull(result);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("r", result.Entries[0].Id);
        Assert.Equal("rosso", result.Entries[0].Value);

        Assert.Equal("g", result.Entries[1].Id);
        Assert.Equal("verde", result.Entries[1].Value);
    }

    [Fact]
    public void CopyThesaurus_Replace_AliasVsNoAlias_Alias()
    {
        Thesaurus source = new()
        {
            Id = "colors@en",
            TargetId = "x@en"
        };

        Thesaurus target = GetRgbThesaurus();

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Replace);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal("x@en", result.TargetId);
    }

    [Fact]
    public void CopyThesaurus_Replace_NoAliasVsAlias_NoAlias()
    {
        Thesaurus source = GetRgbThesaurus();

        Thesaurus target = new()
        {
            Id = "colors@en",
            TargetId = "x@en"
        };

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Replace);

        Assert.NotNull(result);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("r", result.Entries[0].Id);
        Assert.Equal("red", result.Entries[0].Value);
        Assert.Equal("g", result.Entries[1].Id);
        Assert.Equal("green", result.Entries[1].Value);
        Assert.Equal("b", result.Entries[2].Id);
        Assert.Equal("blue", result.Entries[2].Value);
    }

    [Fact]
    public void CopyThesaurus_Replace_AliasVsAlias_Replaced()
    {
        Thesaurus source = new()
        {
            Id = "colors@en",
            TargetId = "source@en"
        };
        Thesaurus target = new()
        {
            Id = "colors@en",
            TargetId = "target@en"
        };

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Replace);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal("source@en", result.TargetId);
    }
    #endregion

    #region Patch
    [Fact]
    public void CopyThesaurus_Patch_NoAliasVsNoAlias_Patched()
    {
        Thesaurus source = new()
        {
            Id = "colors@en"
        };
        source.AddEntry(new ThesaurusEntry { Id = "r", Value = "rosso" });
        source.AddEntry(new ThesaurusEntry { Id = "g", Value = "verde" });

        Thesaurus target = GetRgbThesaurus();

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Patch);

        Assert.NotNull(result);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("r", result.Entries[0].Id);
        Assert.Equal("rosso", result.Entries[0].Value);

        Assert.Equal("g", result.Entries[1].Id);
        Assert.Equal("verde", result.Entries[1].Value);

        Assert.Equal("b", result.Entries[2].Id);
        Assert.Equal("blue", result.Entries[2].Value);
    }

    [Fact]
    public void CopyThesaurus_Patch_AliasVsNoAlias_Alias()
    {
        Thesaurus source = new()
        {
            Id = "colors@en",
            TargetId = "x@en"
        };

        Thesaurus target = GetRgbThesaurus();

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Patch);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal("x@en", result.TargetId);
    }

    [Fact]
    public void CopyThesaurus_Patch_NoAliasVsAlias_NoAlias()
    {
        Thesaurus source = GetRgbThesaurus();

        Thesaurus target = new()
        {
            Id = "colors@en",
            TargetId = "x@en"
        };

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Patch);

        Assert.NotNull(result);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("r", result.Entries[0].Id);
        Assert.Equal("red", result.Entries[0].Value);
        Assert.Equal("g", result.Entries[1].Id);
        Assert.Equal("green", result.Entries[1].Value);
        Assert.Equal("b", result.Entries[2].Id);
        Assert.Equal("blue", result.Entries[2].Value);
    }

    [Fact]
    public void CopyThesaurus_Patch_AliasVsAlias_Patched()
    {
        Thesaurus source = new()
        {
            Id = "colors@en",
            TargetId = "source@en"
        };
        Thesaurus target = new()
        {
            Id = "colors@en",
            TargetId = "target@en"
        };

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Patch);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal("source@en", result.TargetId);
    }
    #endregion

    #region Synch
    [Fact]
    public void CopyThesaurus_Synch_NoAliasVsNoAlias_Synched()
    {
        Thesaurus source = new()
        {
            Id = "colors@en"
        };
        source.AddEntry(new ThesaurusEntry { Id = "r", Value = "rosso" });
        source.AddEntry(new ThesaurusEntry { Id = "g", Value = "verde" });

        Thesaurus target = GetRgbThesaurus();

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Synch);

        Assert.NotNull(result);
        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("r", result.Entries[0].Id);
        Assert.Equal("rosso", result.Entries[0].Value);

        Assert.Equal("g", result.Entries[1].Id);
        Assert.Equal("verde", result.Entries[1].Value);
    }

    [Fact]
    public void CopyThesaurus_Synch_AliasVsNoAlias_Alias()
    {
        Thesaurus source = new()
        {
            Id = "colors@en",
            TargetId = "x@en"
        };

        Thesaurus target = GetRgbThesaurus();

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Synch);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal("x@en", result.TargetId);
    }

    [Fact]
    public void CopyThesaurus_Synch_NoAliasVsAlias_NoAlias()
    {
        Thesaurus source = GetRgbThesaurus();

        Thesaurus target = new()
        {
            TargetId = "x@en"
        };

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
                                  ImportUpdateMode.Synch);

        Assert.NotNull(result);
        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("r", result.Entries[0].Id);
        Assert.Equal("red", result.Entries[0].Value);
        Assert.Equal("g", result.Entries[1].Id);
        Assert.Equal("green", result.Entries[1].Value);
        Assert.Equal("b", result.Entries[2].Id);
        Assert.Equal("blue", result.Entries[2].Value);
    }

    [Fact]
    public void CopyThesaurus_Synch_AliasVsAlias_Synched()
    {
        Thesaurus source = new()
        {
            Id = "colors@en",
            TargetId = "source@en"
        };
        Thesaurus target = new()
        {
            Id = "colors@en",
            TargetId = "target@en"
        };

        Thesaurus result = ThesaurusHelper.CopyThesaurus(source, target,
            ImportUpdateMode.Synch);

        Assert.NotNull(result);
        Assert.Empty(result.Entries);
        Assert.Equal("source@en", result.TargetId);
    }
    #endregion
}