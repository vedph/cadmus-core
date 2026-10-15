using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using Proteus.Core;
using Proteus.Core.Regions;
using Proteus.Entries.Export;
using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cadmus.Import.Proteus;

/// <summary>
/// Item identifiers entry set exporter. This exporter dumps for each item
/// imported its GUID and title, plus its MetadataPart GUID and all the metadata
/// in it with name <c>eid</c>. This is used when you import items in multiple
/// passes and some of them link to others, so you need to know the GUID of the
/// items you imported in the previous passes and those of their MetadataPart,
/// which by convention contains the <c>eid</c> metadata IDs used as internal
/// link targets.
/// <para>Tag: <c>it.vedph.entry-set-exporter.cadmus.item-id</c>.</para>
/// </summary>
/// <remarks>The output file is a JSON file with an array of objects, each
/// having properties <c>id</c> (item GUID), <c>title</c> (item title), and
/// if the item has a MetadataPart, it will also have properties
/// <c>metadataPartId</c> (MetadataPart GUID) and <c>eids</c> (an array of
/// strings with the values of all metadata in this part having
/// name=<c>eid</c>). If the item has more than one MetadataPart, the one
/// without a role is preferred; otherwise, the first one is used.</remarks>
[Tag("it.vedph.entry-set-exporter.cadmus.item-id")]
public sealed class ItemIdEntrySetExporter : EntrySetExporter,
    IConfigurable<ItemIdEntrySetExporterOptions>,
    IEntrySetExporter
{
    /// <summary>
    /// The name of the metadata whose values are exported as <c>eids</c>.
    /// </summary>
    public const string EID_NAME = "eid";

    private ItemIdEntrySetExporterOptions _options = new();
    private FileStream? _stream;
    private Utf8JsonWriter? _writer;

    /// <summary>
    /// Configures this exporter with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(ItemIdEntrySetExporterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
    }

    private async Task ReleaseAsync()
    {
        if (_writer != null)
        {
            await _writer.DisposeAsync();
            _writer = null;
        }
        if (_stream != null)
        {
            await _stream.DisposeAsync();
            _stream = null;
        }
    }

    /// <summary>
    /// Opens the exporter output. Call this once from outside the pipeline,
    /// when you want to start exporting. This will create the output file
    /// (overwriting it if it exists) and start the JSON array. If the exporter
    /// was already open, its previous output is closed first.
    /// </summary>
    /// <exception cref="InvalidOperationException">No output path.</exception>
    protected override async Task DoOpenAsync()
    {
        if (string.IsNullOrWhiteSpace(_options.OutputPath))
        {
            throw new InvalidOperationException("No output path for "
                + nameof(ItemIdEntrySetExporter));
        }

        if (_writer != null) await DoCloseAsync();

        string? dir = Path.GetDirectoryName(Path.GetFullPath(_options.OutputPath));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        _stream = new FileStream(_options.OutputPath, FileMode.Create,
            FileAccess.Write, FileShare.Read, 4096, useAsync: true);
        _writer = new Utf8JsonWriter(_stream, new JsonWriterOptions
        {
            Indented = _options.Indented,
            // the output is a plain JSON file, not embedded in HTML
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        _writer.WriteStartArray();
    }

    /// <summary>
    /// Closes the exporter output. Call this once from outside the pipeline,
    /// when you want to end exporting. This will close the JSON array and
    /// the output file. Calling it when not open does nothing.
    /// </summary>
    protected override async Task DoCloseAsync()
    {
        if (_writer == null) return;

        try
        {
            _writer.WriteEndArray();
            await _writer.FlushAsync();
        }
        finally
        {
            await ReleaseAsync();
        }
    }

    private static MetadataPart? GetMetadataPart(IItem item)
    {
        MetadataPart? part = null;
        foreach (MetadataPart p in item.Parts.OfType<MetadataPart>())
        {
            if (p.RoleId == null) return p;
            part ??= p;
        }
        return part;
    }

    private void WriteItem(IItem item)
    {
        _writer!.WriteStartObject();
        _writer.WriteString("id", item.Id);
        _writer.WriteString("title", item.Title);

        MetadataPart? part = GetMetadataPart(item);
        if (part != null)
        {
            _writer.WriteString("metadataPartId", part.Id);
            _writer.WriteStartArray("eids");
            foreach (Metadatum metadatum in part.Metadata ?? [])
            {
                if (metadatum.Name == EID_NAME &&
                    !string.IsNullOrEmpty(metadatum.Value))
                {
                    _writer.WriteStringValue(metadatum.Value);
                }
            }
            _writer.WriteEndArray();
        }

        _writer.WriteEndObject();
    }

    /// <summary>
    /// Exports the specified entry set.
    /// </summary>
    /// <param name="entrySet">The entry set.</param>
    /// <param name="regionSet">The entry regions set.</param>
    /// <exception cref="ArgumentNullException">entrySet or regionSet</exception>
    /// <exception cref="InvalidOperationException">Exporter not open, or
    /// entry set context is not a <see cref="CadmusEntrySetContext"/>.
    /// </exception>
    protected override async Task DoExportAsync(EntrySet entrySet,
        EntryRegionSet regionSet)
    {
        ArgumentNullException.ThrowIfNull(entrySet);
        ArgumentNullException.ThrowIfNull(regionSet);

        if (_writer == null)
        {
            throw new InvalidOperationException(
                nameof(ItemIdEntrySetExporter) + " is not open");
        }

        if (entrySet.Context is not CadmusEntrySetContext context)
        {
            throw new InvalidOperationException(
                $"{nameof(ItemIdEntrySetExporter)} requires a " +
                nameof(CadmusEntrySetContext));
        }

        if (context.Items.Count == 0) return;

        foreach (IItem item in context.Items) WriteItem(item);

        await _writer.FlushAsync();
    }
}

/// <summary>
/// Options for <see cref="ItemIdEntrySetExporter"/>.
/// </summary>
public class ItemIdEntrySetExporterOptions : IHasDisabled
{
    private string _outputPath = Path.Combine(
        Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory), "item-ids.json");

    /// <summary>
    /// Gets or sets a value indicating whether this exporter is disabled.
    /// </summary>
    public bool IsDisabled { get; set; }

    /// <summary>
    /// Gets or sets the output file path. This can include environment
    /// variables in double braces (e.g.
    /// <c>{{HOMEDRIVE}}{{HOMEPATH}}\Desktop\item-ids.json</c>).
    /// The default is <c>item-ids.json</c> in the user's desktop.
    /// </summary>
    public string OutputPath
    {
        get => _outputPath;
        set => _outputPath = EnvarResolver.ResolveTemplate(value) ?? "";
    }

    /// <summary>
    /// Gets or sets a value indicating whether the JSON output should be
    /// indented. The default is true.
    /// </summary>
    public bool Indented { get; set; } = true;
}
