using Fusi.Tools.Configuration;
using System;
using System.IO;
using System.Text;

namespace Cadmus.Export.ML;

/// <summary>
/// File-based TEI item composer.
/// <para>Tag: <c>it.vedph.item-composer.tei.fs</c>.</para>
/// </summary>
/// <seealso cref="TeiItemComposer" />
[Tag("it.vedph.item-composer.tei.fs")]
public sealed class FSTeiItemComposer : TeiItemComposer, IItemComposer,
    IConfigurable<FSTeiItemComposerOptions>
{
    private FSTeiItemComposerOptions? _options;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(FSTeiItemComposerOptions options)
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

        string head = FillTemplate(_options.TextHead);
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

            string tail = FillTemplate(_options?.TextTail);
            if (!string.IsNullOrEmpty(tail)) p.Value.WriteLine(tail);

            p.Value.Flush();
            p.Value.Close();
        }
        Output.Writers.Clear();

        base.Close();
    }
}

/// <summary>
/// Options for <see cref="FSTeiItemComposer"/>.
/// </summary>
public class FSTeiItemComposerOptions
{
    /// <summary>
    /// Gets or sets the optional text head. This is written at the start
    /// of the text flow. Its value can include placeholders in curly
    /// braces, corresponding to any of the metadata keys defined in
    /// the item composer's context.
    /// </summary>
    public string? TextHead { get; set; }

    /// <summary>
    /// Gets or sets the optional text tail. This is written at the end
    /// of the text flow. Its value can include placeholders in curly
    /// braces, corresponding to any of the metadata keys defined in
    /// the item composer's context.
    /// </summary>
    public string? TextTail { get; set; }

    /// <summary>
    /// Gets or sets the output directory.
    /// </summary>
    public string OutputDirectory { get; set; } = "";

}
