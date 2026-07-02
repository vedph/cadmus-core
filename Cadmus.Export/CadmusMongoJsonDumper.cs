using Fusi.Tools;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using System;
using System.IO;
using System.Text;
using System.Threading;

namespace Cadmus.Export;

/// <summary>
/// Cadmus MongoDB JSON dumper.
/// </summary>
public sealed class CadmusMongoJsonDumper
{
    private readonly CadmusJsonDumperOptions _options;
    private readonly CadmusMongoDataFramer _framer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadmusMongoJsonDumper"/>
    /// class with the specified options.
    /// </summary>
    /// <param name="options">The options used to configure the behavior of
    /// the dumper.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public CadmusMongoJsonDumper(CadmusJsonDumperOptions options)
    {
        _options = options
            ?? throw new ArgumentNullException(nameof(options));
        _framer = new CadmusMongoDataFramer(options);
    }

    private string BuildFileName(int nr)
    {
        string fileName = $"{_options.DatabaseName}";
        if (nr > 0) fileName += $"_{nr:000}";
        fileName += ".json";
        return Path.Combine(_options.OutputDirectory, fileName);
    }

    private void WriteHead(StreamWriter writer, int fileNr,
        JsonWriterSettings jsonSettings)
    {
        // open root object
        writer.WriteLine("{");

        // write time of dump
        writer.WriteLine($"  \"time\": \"{DateTime.UtcNow:O}\",");

        // write chunk if needed
        if (_options.MaxItemsPerFile > 0)
        {
            writer.WriteLine($"  \"chunk\": {fileNr},");
        }

        // write options JSON object
        writer.Write("  \"options\": ");
        writer.Write(_options.ToJson(jsonSettings));
        writer.WriteLine(",");

        // open items array
        writer.WriteLine("  \"items\": [");
    }

    private static void WriteTail(StreamWriter writer)
    {
        // close items array and root object
        writer.WriteLine("]");
        writer.WriteLine("}");
    }

    /// <summary>
    /// Dump the items to JSON files.
    /// </summary>
    /// <param name="filter">The filter.</param>
    /// <param name="cancel">The cancellation token.</param>
    /// <param name="progress">The optional progress reporter.</param>
    /// <returns>Count of items dumped.</returns>
    /// <exception cref="ArgumentNullException">filter</exception>
    public int Dump(CadmusDumpFilter filter, CancellationToken cancel,
        IProgress<ProgressReport>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(filter);

        ProgressReport? report = progress is null ? null : new ProgressReport();

        int count = 0, fileNr = 0;
        StreamWriter? writer = null;
        JsonWriterSettings jsonSettings = new()
        {
            Indent = _options.Indented,
        };

        // get items
        foreach (BsonDocument item in _framer.GetItems(filter))
        {
            count++;

            // create new file for this chunk if needed
            if (writer == null || (_options.MaxItemsPerFile > 0
                && count >= _options.MaxItemsPerFile))
            {
                if (writer != null) WriteTail(writer);
                writer?.Flush();
                writer?.Close();

                string path = BuildFileName(++fileNr);
                writer = new StreamWriter(path, false, Encoding.UTF8);
                WriteHead(writer, fileNr, jsonSettings);
                count = 1;
            }

            // prepend array's item separator if not first item in doc
            if (count > 1) writer.WriteLine(",");

            // write the item as JSON
            string json = item.ToJson(jsonSettings);
            writer.Write(json);

            if (cancel.IsCancellationRequested)
            {
                // if cancelled, close the file and return
                writer.WriteLine();
                writer.Flush();
                writer.Close();
                return count;
            }

            // report progress periodically
            if (progress != null && count % 100 == 0)
            {
                report!.Count = count;
                progress.Report(report);
            }
        }

        if (writer != null) WriteTail(writer);
        writer?.Flush();
        writer?.Close();

        if (progress != null)
        {
            report!.Count = count;
            progress.Report(report);
        }

        return count;
    }
}
