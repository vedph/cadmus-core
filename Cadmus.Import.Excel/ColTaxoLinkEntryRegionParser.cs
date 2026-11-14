using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using Microsoft.Extensions.Logging;
using Proteus.Core;
using Proteus.Core.Regions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Cadmus.Import.Excel;

/// <summary>
/// Taxonomy link column entry region parser. The cell contains one or
/// more labels of taxonomy nodes, each resolved into the corresponding node
/// key in the configured tree, and added as a taxonomy link to the item's
/// <see cref="PinLinksPart"/>. The link GID is <c>@TX:TREEID/NODEKEY</c>,
/// its label is the cell's label, and its scope is the tree ID.
/// <para>Node keys are looked up by label or key (case-insensitively) in the
/// taxonomy CSV files <c>trees.csv</c> and <c>nodes.csv</c> (as used to seed
/// the taxonomies store) found in the configured directory. When not found,
/// a warning is logged and the key is derived from the label.</para>
/// <para>Tag: <c>it.vedph.entry-region-parser.xlsx.col-taxo-link</c>.</para>
/// </summary>
/// <seealso cref="ColEntryRegionParser" />
/// <seealso cref="IEntryRegionParser" />
[Tag("it.vedph.entry-region-parser.xlsx.col-taxo-link")]
public sealed class ColTaxoLinkEntryRegionParser : ColEntryRegionParser,
    IEntryRegionParser, IConfigurable<ColTaxoLinkEntryRegionParserOptions>
{
    private ColTaxoLinkEntryRegionParserOptions? _options;
    // node label or key (lowercase) -> node key
    private Dictionary<string, string>? _keys;

    /// <summary>
    /// Gets the tags of the regions that this parser can handle.
    /// </summary>
    public string[] RegionTags => _options?.Tag != null ? [_options.Tag] : [];

    /// <summary>
    /// Configures this parser.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">Null options.</exception>
    public void Configure(ColTaxoLinkEntryRegionParserOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        ConfigureBaseOptions(options);
        _keys = null;
    }

    private static List<string> ParseCsvLine(string line)
    {
        List<string> fields = [];
        StringBuilder sb = new();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else quoted = false;
                }
                else sb.Append(c);
            }
            else if (c == '"') quoted = true;
            else if (c == ',')
            {
                fields.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(c);
        }
        fields.Add(sb.ToString());
        return fields;
    }

    private static IEnumerable<List<string>> ReadCsv(string path) =>
        File.ReadLines(path, Encoding.UTF8)
            .Skip(1)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(ParseCsvLine);

    private Dictionary<string, string> LoadKeys()
    {
        Dictionary<string, string> keys = new(StringComparer.OrdinalIgnoreCase);
        string? dir = EnvarResolver.ResolveTemplate(_options!.TaxonomyDir);
        if (string.IsNullOrEmpty(dir))
        {
            Logger?.LogWarning("No taxonomy directory configured for {Tag}",
                _options.Tag);
            return keys;
        }

        string treesPath = Path.Combine(dir, "trees.csv");
        string nodesPath = Path.Combine(dir, "nodes.csv");
        if (!File.Exists(treesPath) || !File.Exists(nodesPath))
        {
            Logger?.LogError("Taxonomy files not found in {Directory}", dir);
            return keys;
        }

        // trees: id,name,note,flat; nodes refer to trees by 1-based ordinal
        List<string> trees = [.. ReadCsv(treesPath).Select(f => f[0])];
        int treeN = trees.IndexOf(_options.TreeId!) + 1;
        if (treeN == 0)
        {
            Logger?.LogError("Taxonomy tree {TreeId} not found in {Path}",
                _options.TreeId, treesPath);
            return keys;
        }

        // nodes: tree_n,parent_key,key,label,...
        foreach (List<string> f in ReadCsv(nodesPath))
        {
            if (f.Count < 4 ||
                f[0] != treeN.ToString(CultureInfo.InvariantCulture))
            {
                continue;
            }
            keys.TryAdd(f[3].Trim(), f[2].Trim());
            keys.TryAdd(f[2].Trim(), f[2].Trim());
        }

        Logger?.LogInformation("Loaded {Count} node keys for taxonomy {TreeId}",
            keys.Count, _options.TreeId);
        return keys;
    }

    private static string Slugify(string label)
    {
        StringBuilder sb = new();
        foreach (char c in label.ToLowerInvariant().Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) ==
                UnicodeCategory.NonSpacingMark)
            {
                continue;
            }
            if (char.IsLetterOrDigit(c)) sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '_') sb.Append('_');
        }
        return sb.ToString().TrimEnd('_');
    }

    private string GetNodeKey(string label, EntryRegion region)
    {
        _keys ??= LoadKeys();
        if (_keys.TryGetValue(label, out string? key)) return key;

        key = Slugify(label);
        Logger?.LogWarning("Taxonomy node \"{Label}\" not found in tree " +
            "{TreeId} at region {Region}: using key \"{Key}\"",
            label, _options!.TreeId, region, key);
        return key;
    }

    /// <summary>
    /// Parses the specified column value.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="region">The column region.</param>
    /// <param name="value">The filtered value, or null if empty.</param>
    protected override void ParseValue(CadmusEntrySetContext context,
        EntryRegion region, string? value)
    {
        if (value == null) return;

        if (string.IsNullOrEmpty(_options!.TreeId))
        {
            Logger?.LogError("{Tag} column with no taxonomy tree configured " +
                "at region {Region}", region.Tag, region);
            return;
        }

        IList<string> labels = _options.Separator != null
            ? ImportHelper.GetValueList(value, false, [_options.Separator.Value])
            : [value];

        PinLinksPart part = context.EnsurePartForCurrentItem<PinLinksPart>();
        foreach (string label in labels)
        {
            part.Links.Add(new AssertedCompositeId
            {
                Scope = _options.Scope ?? _options.TreeId,
                Target = new PinTarget
                {
                    Gid = $"@TX:{_options.TreeId}/{GetNodeKey(label, region)}",
                    Label = label
                }
            });
        }
    }
}

/// <summary>
/// Options for <see cref="ColTaxoLinkEntryRegionParser"/>.
/// </summary>
public class ColTaxoLinkEntryRegionParserOptions : ColEntryRegionParserOptions
{
    /// <summary>
    /// Gets or sets the taxonomy tree ID (e.g. <c>gods</c>).
    /// </summary>
    public string? TreeId { get; set; }

    /// <summary>
    /// Gets or sets the directory with the taxonomy files <c>trees.csv</c>
    /// and <c>nodes.csv</c>. You can use environment variables between
    /// double curly brackets, e.g. <c>{{HOMEDRIVE}}{{HOMEPATH}}\\Desktop</c>.
    /// </summary>
    public string? TaxonomyDir { get; set; }

    /// <summary>
    /// Gets or sets the optional separator for multiple values in the cell.
    /// When null, the cell has a single value.
    /// </summary>
    public char? Separator { get; set; }

    /// <summary>
    /// Gets or sets the scope of the links. When null, the tree ID is used.
    /// </summary>
    public string? Scope { get; set; }
}
