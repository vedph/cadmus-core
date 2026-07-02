using Cadmus.Core;
using Fusi.Tools.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Proteus.Core;
using Proteus.Core.Entries;
using Proteus.Core.Regions;
using Proteus.Entries.Export;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Import.Proteus;

/// <summary>
/// Markdown dump entry set exporter. This exporter dumps the entry set
/// into a set of output files.
/// <para>Tag: <c>it.vedph.entry-set-exporter.cadmus.md-dump</c>.</para>
/// </summary>
[Tag("it.vedph.entry-set-exporter.cadmus.md-dump")]
public sealed class MdDumpEntrySetExporter : EntrySetExporter,
    IConfigurable<MdDumpEntrySetExporterOptions>,
    IEntrySetExporter
{
    private int _fileNr;
    private int _setCount;
    private MdDumpEntrySetExporterOptions _options;
    private TextWriter? _writer;
    private DecodedEntryDumper? _dumper;
    private readonly JsonSerializerSettings _jsonSettings;

    /// <summary>
    /// Initializes a new instance of the <see cref="MdDumpEntrySetExporter"/>
    /// class.
    /// </summary>
    public MdDumpEntrySetExporter()
    {
        _options = new();
        _jsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };
    }

    /// <summary>
    /// Configures this exporter with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(MdDumpEntrySetExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Opens the exporter output. Call this once from outside the pipeline,
    /// when you want to start exporting. This will create the first output file.
    /// </summary>
    protected override Task DoOpenAsync()
    {
        _fileNr = 1;
        _setCount = 0;

        if (!string.IsNullOrEmpty(_options.OutputDirectory) &&
            !Directory.Exists(_options.OutputDirectory))
        {
            Directory.CreateDirectory(_options.OutputDirectory);
        }
        string path = Path.Combine(_options.OutputDirectory ?? "", "d00001.md");

        _writer = new StreamWriter(path, false, Encoding.UTF8);
        _dumper = new DecodedEntryDumper(new CsvDecodedEntryDataWriter(
            _writer, _options));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Closes the exporter output. Call this once from outside the pipeline,
    /// when you want to end exporting. This will close the output file.
    /// </summary>
    protected override Task DoCloseAsync()
    {
        _writer?.Flush();
        _dumper?.Dispose();
        _dumper = null;
        return Task.CompletedTask;
    }

    private void DumpItems(CadmusEntrySetContext context)
    {
        if (context.Items.Count == 0) return;

        _writer!.WriteLine($"### {context.Number} - Items");
        int n = 0;
        foreach (IItem item in context.Items)
        {
            _writer.WriteLine();
            _writer.WriteLine($"#### {++n}: {item.Title}");
            _writer.WriteLine();
            _writer.WriteLine($"- ID: `{item.Id}`");
            _writer.WriteLine($"- description: `{item.Description}`");
            _writer.WriteLine($"- facet ID: `{item.FacetId}`");
            if (!string.IsNullOrEmpty(item.GroupId))
                _writer.WriteLine($"- group ID: `{item.GroupId}`");
            _writer.WriteLine($"- sort key: `{item.SortKey}`");
            _writer.WriteLine($"- created: `{item.TimeCreated}`");
            _writer.WriteLine($"- creator: `{item.CreatorId}`");
            _writer.WriteLine($"- modified: `{item.TimeModified}`");
            _writer.WriteLine($"- user: `{item.UserId}`");
            if (item.Flags != 0)
                _writer.WriteLine($"- flags: `{item.Flags:X4}`");
            if (item.Parts.Count > 0)
            {
                _writer.WriteLine($"- parts: {item.Parts.Count}");
                _writer.WriteLine();

                int pn = 0;
                foreach (IPart part in item.Parts)
                {
                    _writer.WriteLine(
                        $"- part __{++pn}__ / {item.Parts.Count}: {part}");

                    if (_options.JsonParts)
                    {
                        string json = JsonConvert.SerializeObject(part,
                            _jsonSettings);
                        _writer.WriteLine();
                        _writer.WriteLine("```json");
                        _writer.WriteLine(json);
                        _writer.WriteLine("```");
                        _writer.WriteLine();
                    }
                }
            }
            else
            {
                _writer.WriteLine();
            }
        }
    }

    /// <summary>
    /// Exports the specified entry set.
    /// </summary>
    /// <param name="entrySet">The entry set.</param>
    /// <param name="regionSet">The entry regions set.</param>
    protected override async Task DoExportAsync(EntrySet entrySet,
        EntryRegionSet regionSet)
    {
        ArgumentNullException.ThrowIfNull(entrySet);
        ArgumentNullException.ThrowIfNull(regionSet);

        // create a new file if required
        if (_options.MaxEntriesPerDumpFile > 0 &&
            ++_setCount > _options.MaxEntriesPerDumpFile)
        {
            _setCount = 1;
            string path = Path.Combine(_options.OutputDirectory ?? "",
                $"d{++_fileNr:00000}.md");
            await CloseAsync();
            _writer = new StreamWriter(path, false, Encoding.UTF8);
            _dumper = new DecodedEntryDumper(new CsvDecodedEntryDataWriter(
                _writer, _options));
        }

        // dump the set
        CadmusEntrySetContext context = (CadmusEntrySetContext)entrySet.Context;
        await _writer!.WriteLineAsync(new string('-', 60));
        await _writer.WriteLineAsync($"## {context.Number}");
        await _writer.WriteLineAsync();

        // data
        if (context.Data.Count > 0)
        {
            await _writer.WriteLineAsync($"### {context.Number} - Data");
            await _writer.WriteLineAsync();
            foreach (string key in context.Data.Keys.Order())
            {
                await _writer.WriteAsync(key[0] == '*' ? "- \\" : "- ");
                await _writer.WriteLineAsync($"{key}=`{context.Data[key]}`");
            }

            await _writer.WriteLineAsync();
        }

        // items with their parts
        DumpItems(context);

        // entries
        if (!_options.NoEntries)
        {
            await _writer.WriteLineAsync($"### {context.Number} - Entries");
            await _writer.WriteLineAsync();
            await _writer.WriteLineAsync("```tsv");
            _dumper!.Dump(context.Number, entrySet.Entries, regionSet.Regions);
            await _writer.WriteLineAsync("```");
        }

        await _writer.WriteLineAsync();
    }
}

/// <summary>
/// Options for <see cref="MdDumpEntrySetExporter"/>.
/// </summary>
public class MdDumpEntrySetExporterOptions : DecodedEntryDataWriterOptions,
    IHasDisabled
{
    private string _outputDir = "";

    /// <summary>
    /// Gets or sets a value indicating whether this exporter is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the output directory. This can include environment
    /// variables in double braces (e.g.
    /// <c>{{HOMEDRIVE}}{{HOMEPATH}}\Desktop\out\</c>).
    /// </summary>
    public string OutputDirectory
    {
        get => _outputDir;
        set => _outputDir = EnvarResolver.ResolveTemplate(value) ?? "";
    }

    /// <summary>
    /// Gets or sets the maximum number of sets per file.
    /// </summary>
    public int MaxEntriesPerDumpFile { get; set; } = 100;

    /// <summary>
    /// Gets or sets a value indicating whether to exclude entries from dump.
    /// </summary>
    public bool NoEntries { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to dump JSON code for each part
    /// being dumped.
    /// </summary>
    public bool JsonParts { get; set; }
}