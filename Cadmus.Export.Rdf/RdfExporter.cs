using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Cadmus.Export.Rdf;

/// <summary>
/// Main class for exporting RDF data from the Cadmus Graph database.
/// </summary>
public sealed class RdfExporter
{
    private readonly RdfDataReader _dataReader;
    private readonly RdfExportSettings _settings;

    /// <summary>
    /// Occurs when progress is reported during an RDF export operation.
    /// </summary>
    /// <remarks>This event is triggered periodically to provide updates on
    /// the progress of an RDF export. 
    /// The event handler receives an <see cref="RdfExportProgress"/> object
    /// containing details about the current progress.</remarks>
    public event Action<RdfExportProgress>? OnProgressReported;

    /// <summary>
    /// Creates a new instance of <see cref="RdfExporter"/>.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="settings">The export settings.</param>
    /// <exception cref="ArgumentNullException">connectionString</exception>
    public RdfExporter(string connectionString,
        RdfExportSettings? settings = null)
    {
        if (string.IsNullOrEmpty(connectionString))
            throw new ArgumentNullException(nameof(connectionString));

        _dataReader = new RdfDataReader(connectionString);
        _settings = settings ?? new RdfExportSettings();
    }

    private async Task<Dictionary<string, string>> LoadPrefixMappingsAsync()
    {
        List<NamespaceMapping> mappings =
            await _dataReader.GetNamespaceMappingsAsync();
        return mappings.ToDictionary(m => m.Prefix, m => m.Uri);
    }

    private async Task<Dictionary<int, string>> LoadUriMappingsAsync()
    {
        List<UriMapping> mappings = await _dataReader.GetUriMappingsAsync();
        return mappings.ToDictionary(m => m.Id, m => m.Uri);
    }

    /// <summary>
    /// Creates and returns a properly configured RDF writer.
    /// Useful for testing scenarios where you want to access the writer directly.
    /// </summary>
    public async Task<RdfWriter> CreateWriterAsync()
    {
        Dictionary<string, string> prefixMappings =
            await LoadPrefixMappingsAsync();
        Dictionary<int, string> uriMappings =
            await LoadUriMappingsAsync();
        return RdfWriterFactory.CreateWriter(_settings.Format,
            _settings, prefixMappings, uriMappings);
    }

    private async Task ExportWithWriterAsync(TextWriter textWriter,
        RdfWriter rdfWriter)
    {
        // write header
        await rdfWriter.WriteHeaderAsync(textWriter);

        // export triples in batches
        int totalTriples = await _dataReader.GetTripleCountAsync(_settings);
        int processedTriples = 0;

        while (processedTriples < totalTriples)
        {
            List<RdfTriple> batch = await _dataReader.GetTriplesAsync(
                _settings, processedTriples, _settings.BatchSize);
            if (batch.Count == 0) break;

            await rdfWriter.WriteAsync(textWriter, batch);
            processedTriples += batch.Count;

            // report progress
            OnProgressReported?.Invoke(new RdfExportProgress
            {
                ProcessedTriples = processedTriples,
                TotalTriples = totalTriples,
                PercentComplete = (double)processedTriples / totalTriples * 100
            });
        }

        // write footer
        await rdfWriter.WriteFooterAsync(textWriter);
    }

    /// <summary>
    /// Exports RDF data to the specified output path.
    /// </summary>
    /// <param name="outputPath">The output file path.</param>
    public async Task ExportAsync(string outputPath)
    {
        ArgumentNullException.ThrowIfNull(outputPath);

        using FileStream fileStream = new(outputPath, FileMode.Create,
            FileAccess.Write);
        using StreamWriter writer = new(fileStream, _settings.Encoding);
        await ExportAsync(writer);
    }

    /// <summary>
    /// Exports RDF data to the specified <see cref="TextWriter"/> in the
    /// configured format.
    /// </summary>
    /// <param name="writer">The <see cref="TextWriter"/> to which the RDF data
    /// will be written.</param>
    /// <exception cref="ArgumentNullException">writer</exception>
    public async Task ExportAsync(TextWriter writer)
    {
        ArgumentNullException.ThrowIfNull(writer);

        // load mappings
        Dictionary<string, string> prefixMappings =
            await LoadPrefixMappingsAsync();
        Dictionary<int, string> uriMappings = await LoadUriMappingsAsync();

        // create the appropriate writer based on settings
        RdfWriter rdfWriter = RdfWriterFactory.CreateWriter(_settings.Format,
            _settings, prefixMappings, uriMappings);

        await ExportWithWriterAsync(writer, rdfWriter);
    }

    /// <summary>
    /// Exports RDF data using a provided RDF writer instance.
    /// This is useful for testing or when you need direct access to the writer.
    /// </summary>
    /// <exception cref="ArgumentNullException">textWriter or rdfWriter</exception>
    public async Task ExportAsync(TextWriter textWriter, RdfWriter rdfWriter)
    {
        ArgumentNullException.ThrowIfNull(textWriter);
        ArgumentNullException.ThrowIfNull(rdfWriter);

        await ExportWithWriterAsync(textWriter, rdfWriter);
    }
}

/// <summary>
/// Progress information for RDF export operation.
/// </summary>
public class RdfExportProgress
{
    /// <summary>
    /// The number of triples processed so far.
    /// </summary>
    public int ProcessedTriples { get; set; }

    /// <summary>
    /// The total number of triples to be processed.
    /// </summary>
    public int TotalTriples { get; set; }

    /// <summary>
    /// The percentage of completion for the export operation.
    /// </summary>
    public double PercentComplete { get; set; }

    /// <summary>
    /// String representation of the progress.
    /// </summary>
    /// <returns>String.</returns>
    public override string ToString()
    {
        return $"{ProcessedTriples}/{TotalTriples} " +
            $"({PercentComplete:F2}%)";
    }
}
