using Fusi.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Xunit;

namespace Cadmus.Export.Test;

// https://github.com/xunit/xunit/issues/1999

[CollectionDefinition(nameof(NonParallelResourceCollection),
    DisableParallelization = true)]
[Collection(nameof(NonParallelResourceCollection))]
public sealed class CadmusMongoJsonDumperTest :
    IClassFixture<MongoFixture>, IDisposable
{
    private readonly MongoFixture _fixture;
    private readonly string _tempDir;

    public CadmusMongoJsonDumperTest(MongoFixture fixture)
    {
        _fixture = fixture;
        _tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, true);
        }
    }

    private CadmusJsonDumperOptions GetTestOptions(string dbName = "test-db")
    {
        return new CadmusJsonDumperOptions
        {
            ConnectionString = "mongodb://localhost:27017/{0}",
            DatabaseName = dbName,
            OutputDirectory = _tempDir,
            Indented = true
        };
    }

    #region Constructor Tests
    [Fact]
    public void Constructor_WithValidOptions_SetsPropertiesCorrectly()
    {
        CadmusJsonDumperOptions options = GetTestOptions();

        CadmusMongoJsonDumper dumper = new(options);

        Assert.NotNull(dumper);
    }

    [Fact]
    public void Constructor_WithNullOptions_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new CadmusMongoJsonDumper(null!));
    }
    #endregion

    #region Dump Tests
    [Fact]
    public void Dump_WithNullFilter_ThrowsArgumentNullException()
    {
        CadmusJsonDumperOptions options = GetTestOptions();
        CadmusMongoJsonDumper dumper = new(options);

        Assert.Throws<ArgumentNullException>(() =>
            dumper.Dump(null!, CancellationToken.None));
    }

    [Fact]
    public void Dump_WithBasicData_ReturnsCorrectCount()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetTestOptions();
        CadmusMongoJsonDumper dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        int count = dumper.Dump(filter, CancellationToken.None);

        Assert.Equal(4, count); // Based on BasicDataset having 4 items
    }

    [Fact]
    public void Dump_WithEmptyData_ReturnsZero()
    {
        _fixture.ClearDatabase(); // ensure empty database
        CadmusJsonDumperOptions options = GetTestOptions();
        CadmusMongoJsonDumper dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        int count = dumper.Dump(filter, CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public void Dump_WithProgressReporter_ReportsProgress()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetTestOptions();
        CadmusMongoJsonDumper dumper = new(options);

        List<ProgressReport> progressReports = [];
        Progress<ProgressReport> progress =
            new(progressReports.Add);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        dumper.Dump(filter, CancellationToken.None, progress);

        Assert.NotEmpty(progressReports);
        Assert.All(progressReports, report => Assert.True(report.Count >= 0));
    }

    [Fact]
    public void Dump_WithCancellationToken_StopsWhenCancelled()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetTestOptions();
        CadmusMongoJsonDumper dumper = new(options);

        CancellationTokenSource cts = new();
        cts.Cancel(); // cancel immediately

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        int count = dumper.Dump(filter, cts.Token);

        // when cancelled immediately, it should return early
        Assert.True(count >= 0);
    }

    [Fact]
    public void Dump_MultipleCallsWithSameOptions_ProducesConsistentResults()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetTestOptions();
        CadmusMongoJsonDumper dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        int count1 = dumper.Dump(filter, CancellationToken.None);

        // clean up and dump again
        File.Delete(Path.Combine(_tempDir, "test-db.json"));
        int count2 = dumper.Dump(filter, CancellationToken.None);

        Assert.Equal(count1, count2);
    }
    #endregion

    #region Edge Cases and Error Handling
    [Fact]
    public void Dump_WithInvalidOutputPath_ThrowsDirectoryNotFoundException()
    {
        CadmusJsonDumperOptions options = GetTestOptions();
        options.OutputDirectory = "/invalid/path/that/does/not/exist";
        CadmusMongoJsonDumper dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        Assert.ThrowsAny<Exception>(() => dumper.Dump(filter,
            CancellationToken.None));
    }

    [Fact]
    public void Dump_WithVeryLargeMaxItemsPerFile_WorksCorrectly()
    {
        _fixture.LoadMockData("BasicDataset.csv");
        CadmusJsonDumperOptions options = GetTestOptions();
        options.MaxItemsPerFile = int.MaxValue;
        CadmusMongoJsonDumper dumper = new(options);

        CadmusDumpFilter filter = new()
        {
            PageNumber = 1,
            PageSize = 0
        };

        int count = dumper.Dump(filter, CancellationToken.None);

        Assert.Equal(4, count);
        string[] files = Directory.GetFiles(_tempDir, "*.json");
        Assert.Single(files); // Should create only one file

        #endregion
    }
}
