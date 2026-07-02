using Cadmus.Core;
using Fusi.Tools.Configuration;
using System;
using System.IO;
using System.Text;

namespace Cadmus.Export.ML;

/// <summary>
/// File-based TEI standoff item composer. This just saves the text flows
/// produced by a text item with its layers into a set of XML documents.
/// <para>Tag: <c>it.vedph.item-composer.tei-standoff.fs</c>.</para>
/// </summary>
/// <seealso cref="ItemComposer" />
/// <seealso cref="IItemComposer" />
[Tag("it.vedph.item-composer.tei-standoff.fs")]
public sealed class FSTeiOffItemComposer : TeiOffItemComposer,
    IItemComposer, IConfigurable<FSTeiStandoffItemComposerOptions>
{
    private FSTeiStandoffItemComposerOptions? _options;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(FSTeiStandoffItemComposerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Ensures the writer with the specified key exists in
    /// <see cref="P:Cadmus.Export.ItemComposer.Output" />,
    /// creating it if required.
    /// </summary>
    /// <param name="key">The writer's key.</param>
    /// <exception cref="ArgumentNullException">key</exception>
    protected override void EnsureWriter(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (Output?.Writers.ContainsKey(key) != false) return;

        if (!string.IsNullOrEmpty(_options!.OutputDirectory) &&
            !Directory.Exists(_options.OutputDirectory))
        {
            Directory.CreateDirectory(_options.OutputDirectory ?? "");
        }
        Output.Writers[key] = new StreamWriter(
            Path.Combine(_options!.OutputDirectory ?? "",
            key.Replace('|', '_') + ".xml"),
            false,
            Encoding.UTF8);

        string head = key == PartBase.BASE_TEXT_ROLE_ID
            ? FillTemplate(_options.TextHead)
            : FillTemplate(_options.LayerHead);
        WriteOutput(key, head);
    }

    /// <summary>
    /// Close the composer.
    /// </summary>
    public override void Close()
    {
        if (Output == null) return;

        foreach (var p in Output.Writers)
        {
            Context.Data[M_FLOW_KEY] = p.Key;

            string tail = p.Key == PartBase.BASE_TEXT_ROLE_ID
                ? FillTemplate(_options?.TextTail)
                : FillTemplate(_options?.LayerTail);
            if (!string.IsNullOrEmpty(tail)) p.Value.WriteLine(tail);

            p.Value.Flush();
            p.Value.Close();
        }
        Output.Writers.Clear();

        base.Close();
    }
}

/// <summary>
/// Options for <see cref="FSTeiOffItemComposer"/>.
/// </summary>
public class FSTeiStandoffItemComposerOptions : TeiOffItemComposerOptions
{
    /// <summary>
    /// Gets or sets the output directory.
    /// </summary>
    public string OutputDirectory { get; set; }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="FSTeiStandoffItemComposerOptions"/> class.
    /// </summary>
    public FSTeiStandoffItemComposerOptions()
    {
        OutputDirectory = "";
    }
}
