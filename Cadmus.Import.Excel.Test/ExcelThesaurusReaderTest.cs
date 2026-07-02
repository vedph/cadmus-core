using Cadmus.Core.Config;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace Cadmus.Import.Excel.Test;

public sealed class ExcelThesaurusReaderTest
{
    private static Stream GetResourceStream(string name)
    {
        return Assembly.GetExecutingAssembly().GetManifestResourceStream(
            "Cadmus.Import.Excel.Test.Assets." + name)!;
    }

    [Fact]
    public void Read_TwoThesauri_Ok()
    {
        ExcelThesaurusReader reader = new(GetResourceStream("Book1.xlsx"),
            new ExcelThesaurusReaderOptions { RowOffset = 1 });

        // colors thesaurus
        Assert.True(reader.Next());
        Thesaurus thesaurus = reader.Current!;
        Assert.NotNull(thesaurus);
        Assert.Equal("colors@en", thesaurus!.Id);
        Assert.Null(thesaurus.TargetId);
        Assert.Equal(3, thesaurus.Entries.Count);
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "r" && e.Value == "red"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "g" && e.Value == "green"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "b" && e.Value == "blue"));

        // shapes thesaurus
        Assert.True(reader.Next());
        thesaurus = reader.Current!;
        Assert.NotNull(thesaurus);
        Assert.Equal("shapes@en", thesaurus!.Id);
        Assert.Null(thesaurus.TargetId);
        Assert.Equal(2, thesaurus.Entries.Count);
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "trg" && e.Value == "triangle"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "rct" && e.Value == "rectangle"));
    }

    [Fact]
    public void Read_TwoThesauriWithImplicitId_Ok()
    {
        ExcelThesaurusReader reader = new(GetResourceStream("Book2.xlsx"),
            new ExcelThesaurusReaderOptions { RowOffset = 1 });

        // colors thesaurus
        Assert.True(reader.Next());
        Thesaurus thesaurus = reader.Current!;
        Assert.Equal("colors@en", thesaurus!.Id);
        Assert.Null(thesaurus.TargetId);
        Assert.Equal(3, thesaurus.Entries.Count);
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "r" && e.Value == "red"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "g" && e.Value == "green"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "b" && e.Value == "blue"));

        // shapes thesaurus
        Assert.True(reader.Next());
        thesaurus = reader.Current!;
        Assert.Equal("shapes@en", thesaurus!.Id);
        Assert.Null(thesaurus.TargetId);
        Assert.Equal(2, thesaurus.Entries.Count);
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "trg" && e.Value == "triangle"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "rct" && e.Value == "rectangle"));
    }

    [Fact]
    public void Read_ThesaurusAndAlias_Ok()
    {
        ExcelThesaurusReader reader = new(GetResourceStream("Book3.xlsx"),
            new ExcelThesaurusReaderOptions { RowOffset = 1 });

        // colors thesaurus
        Assert.True(reader.Next());
        Thesaurus thesaurus = reader.Current!;
        Assert.Equal("colors@en", thesaurus!.Id);
        Assert.Null(thesaurus.TargetId);
        Assert.Equal(3, thesaurus.Entries.Count);
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "r" && e.Value == "red"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "g" && e.Value == "green"));
        Assert.NotNull(thesaurus.Entries
            .FirstOrDefault(e => e.Id == "b" && e.Value == "blue"));

        // alias thesaurus
        Assert.True(reader.Next());
        thesaurus = reader.Current!;
        Assert.NotNull(thesaurus);
        Assert.Equal("colours@en", thesaurus!.Id);
        Assert.Equal("colors", thesaurus.TargetId);
        Assert.Empty(thesaurus.Entries);
    }
}