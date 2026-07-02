using Cadmus.Core;
using Cadmus.Core.Storage;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Reflection;
using Cadmus.Core.Layers;
using Fusi.Tools.Text;
using Proteus.Core.Text;
using Fusi.Tools;

namespace Cadmus.Export.Filters;

/// <summary>
/// Mongo-based text extractor for token-based text parts.
/// This replaces all the text locations matched via a specified regular
/// expression pattern with the corresponding text from the base text part.
/// <para>Tag: <c>it.vedph.text-filter.str.mongo-token-extractor</c>.
/// Old tag: <c>it.vedph.renderer-filter.mongo-token-extractor</c>.</para>
/// </summary>
[Tag("it.vedph.text-filter.str.mongo-token-extractor")]
public sealed class MongoTokenExtractorTextFilter : TextFilter<string>,
    IConfigurable<MongoTokenExtractorRendererFilterOptions>
{
    private MongoTokenExtractorRendererFilterOptions? _options;
    private Regex? _locRegex;

    private string? _itemId;
    private TokenTextPart? _part;

    /// <summary>
    /// Configures this filter with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(MongoTokenExtractorRendererFilterOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _locRegex = new Regex(_options.LocationPattern, RegexOptions.Compiled);
    }

    private static TokenTextPart? GetTokenTextPart(string itemId,
        ICadmusRepository repository)
    {
        IItem? item = repository!.GetItem(itemId);
        if (item == null) return null;

        IPart? part = repository.GetItemParts([itemId],
            typeof(TokenTextPart).GetCustomAttribute<TagAttribute>()?.Tag,
            PartBase.BASE_TEXT_ROLE_ID).FirstOrDefault();

        return part == null ? null : (TokenTextPart)part;
    }

    /// <summary>
    /// Applies this filter to the specified text.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="context">The optional context.</param>
    /// <returns>Filtered text or null.</returns>
    protected override object? DoApply(string? text,
        IHasDataDictionary? context = null)
    {
        if (_locRegex == null || string.IsNullOrEmpty(text)) return text;

        // get item ID
        string? itemId = context?.Data?.ContainsKey(
            ItemComposer.M_ITEM_ID) == true
            ? context!.Data[ItemComposer.M_ITEM_ID] as string
            : null;
        if (itemId == null) return text;

        ICadmusRepository? repository =
            (context as CadmusRendererContext)?.Repository;
        if (repository == null) return text;

        // get base text part
        TokenTextPart? part;
        if (_itemId == itemId)
        {
            part = _part!;
        }
        else
        {
            part = GetTokenTextPart(itemId, repository);
            if (part == null) return text;
            // cache the item's base text part
            _itemId = itemId;
            _part = part;
        }

        return _locRegex.Replace(text, (m) =>
        {
            string loc = m.Groups[1].Value;
            if (string.IsNullOrEmpty(loc)) return m.Groups[1].Value;

            // extract
            TokenTextLocation tl = TokenTextLocation.Parse(loc);
            string text = part.GetText(tl,
                _options?.WholeToken == true,
                _options?.StartMarker!,
                _options?.EndMarker!);

            // cut if required
            if (_options?.TextCutting == true)
                text = TextCutter.Cut(text, _options)!;

            // insert into template if required
            if (!string.IsNullOrEmpty(_options?.TextTemplate))
                text = _options.TextTemplate.Replace("{text}", text);

            return text;
        });
    }
}

/// <summary>
/// Options for <see cref="MongoTokenExtractorTextFilter"/>.
/// </summary>
public class MongoTokenExtractorRendererFilterOptions : TextCutterOptions
{
    /// <summary>
    /// Gets or sets the connection string to the Mongo database
    /// containing the thesauri.
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Gets or sets the regular expression pattern representing a text
    /// location expression. It is assumed that the first capture group
    /// in it is the text location.
    /// </summary>
    public string LocationPattern { get; set; } = @"\@{([^}]+)}";

    /// <summary>
    /// Gets or sets the optional text template. When not specified, the
    /// location matched with <see cref="LocationPattern"/> is just replaced
    /// with the extracted text; when specified, the location is replaced
    /// with this template, where the placeholder <c>{text}</c> represents
    /// the extracted text. Default is null.
    /// </summary>
    public string? TextTemplate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to extract the whole token
    /// from the base text, even when the oordinates refer to a portion of
    /// it.
    /// </summary>
    public bool WholeToken { get; set; }

    /// <summary>
    /// Gets or sets the start marker to insert at the beginning of the
    /// token portion when extracting the whole token. Default is <c>[</c>.
    /// </summary>
    public string? StartMarker { get; set; } = "[";

    /// <summary>
    /// Gets or sets the end marker to insert at the beginning of the
    /// token portion when extracting the whole token. Default is <c>]</c>.
    /// </summary>
    public string? EndMarker { get; set; } = "]";

    /// <summary>
    /// Gets or sets a value indicating whether text cutting is enabled.
    /// </summary>
    public bool TextCutting { get; set; }
}
