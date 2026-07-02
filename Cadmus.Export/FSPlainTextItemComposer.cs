using Cadmus.Core;
using Fusi.Tools.Configuration;
using Fusi.Tools.Data;
using Proteus.Rendering;
using System;
using System.IO;
using System.Text;

namespace Cadmus.Export;

/// <summary>
/// File-based plain text item composer. This is essentially used to export
/// plain text documents from a text item into a single file, or one file
/// per items group.
/// <para>Tag: <c>it.vedph.item-composer.txt.fs</c>.</para>
/// </summary>
[Tag("it.vedph.item-composer.txt.fs")]
public sealed class FSPlainTextItemComposer : ItemComposer, IItemComposer,
    IConfigurable<FSPlainTextItemComposerOptions>
{
    private FSPlainTextItemComposerOptions? _options;
    private string? _fileName;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(FSPlainTextItemComposerOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Ensures the writer with the specified key exists in
    /// <see cref="ItemComposer.Output" />, creating it if required.
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
            Path.Combine(_options!.OutputDirectory ?? "", key + ".txt"),
            false,
            Encoding.UTF8);

        WriteOutput(key, FillTemplate(_options.TextHead));
    }

    /// <summary>
    /// Invoked when the item's group changed since the last call to
    /// <see cref="ItemComposer.Compose" />. This can be used when processing
    /// grouped items in order.
    /// </summary>
    /// <param name="item">The new item.</param>
    /// <param name="prevGroupId">The previous group identifier.</param>
    protected override void OnGroupChanged(IItem item, string? prevGroupId)
    {
        // ignore if not grouping items
        if (_options?.ItemGrouping != true) return;

        // close previous writer if any and set new filename
        if (_fileName != null) Output?.FlushWriters(true);
        _fileName = item.GroupId;
    }

    /// <summary>
    /// Does the composition for the specified item.
    /// </summary>
    protected override void DoCompose()
    {
        if (Output == null || Context.Source == null) return;

        // first time we must build the filename
        IItem item = (IItem)Context.Source!;
        _fileName ??= SanitizeFileName(
                _options!.ItemGrouping && !string.IsNullOrEmpty(item.GroupId)
                ? item.GroupId
                : item.Title);

        // item head if any
        if (!string.IsNullOrEmpty(_options!.ItemHead))
            WriteOutput(_fileName, FillTemplate(_options.ItemHead));

        // text: there must be one
        IPart? textPart = item.Parts.Find(
            p => p.RoleId == PartBase.BASE_TEXT_ROLE_ID);
        if (textPart == null || TextPartFlattener == null ||
            TextTreeRenderer == null)
        {
            return;
        }

        TreeNode<ExportedSegment>? tree = BuildTextTree(item);
        if (tree == null) return;

        // render blocks
        string? result = TextTreeRenderer.Render(tree, Context);
        if (result != null) WriteOutput(_fileName, result);

        // item tail if any
        if (!string.IsNullOrEmpty(_options!.ItemTail))
            WriteOutput(_fileName, FillTemplate(_options.ItemTail));
    }

    /// <summary>
    /// Close the composer.
    /// </summary>
    public override void Close()
    {
        if (_fileName != null && !string.IsNullOrEmpty(_options!.TextTail))
        {
            WriteOutput(_fileName, FillTemplate(_options.TextTail));
        }
        base.Close();
    }
}

/// <summary>
/// Options for <see cref="FSPlainTextItemComposer"/>.
/// </summary>
public class FSPlainTextItemComposerOptions
{
    /// <summary>
    /// Gets or sets the optional text to write before each item. Its value
    /// can include placeholders in curly braces, corresponding to any of
    /// the metadata keys defined in the item composer's context.
    /// </summary>
    public string? ItemHead { get; set; }

    /// <summary>
    /// Gets or sets the optional text to write after each item. Its value
    /// can include placeholders in curly braces, corresponding to any of
    /// the metadata keys defined in the item composer's context.
    /// </summary>
    public string? ItemTail { get; set; }

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
    /// Gets or sets a value indicating whether item grouping is enabled.
    /// When enabled, whenever the group ID of the processed item changes
    /// in relation with the group ID of the latest processed item, a new
    /// file is created.
    /// </summary>
    public bool ItemGrouping { get; set; }

    /// <summary>
    /// Gets or sets the output directory.
    /// </summary>
    public string OutputDirectory { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FSPlainTextItemComposerOptions"/>
    /// class.
    /// </summary>
    public FSPlainTextItemComposerOptions()
    {
        OutputDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.DesktopDirectory);
    }
}
