using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf.Test;

public class NonParallelResourceCollection { }

// https://github.com/xunit/xunit/issues/1999
[CollectionDefinition(nameof(NonParallelResourceCollection),
    DisableParallelization = true)]

[Collection(nameof(NonParallelResourceCollection))]
public sealed class RdfExporterTest
{
    [Fact]
    public async Task Export_DefaultSettings_Ok()
    {
        TestHelper.DropDatabase();
        TestHelper.CreateDatabase();

        string cs = TestHelper.GetConnectionString();

        //RdfExportSettings settings = new() { Format = "ram" };
        //RdfExporter exporter = new(cs, settings);
        //await exporter.ExportAsync("output.ttl");

        RdfExportSettings settings = new() { Format = "ram" };
        RdfExporter exporter = new(cs, settings);

        // create the writer yourself
        RamRdfWriter writer = (RamRdfWriter)await exporter.CreateWriterAsync();

        // export using your writer instance
        using MemoryStream ms = new();
        using StreamWriter sw = new(ms, Encoding.UTF8);
        
        await exporter.ExportAsync(sw, writer);
        
        string output = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Equal(3, writer.Statistics.LiteralTripleCount);
        // TODO
    }
}
