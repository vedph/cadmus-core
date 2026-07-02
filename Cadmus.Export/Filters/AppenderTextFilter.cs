using Fusi.Tools;
using Fusi.Tools.Configuration;
using Proteus.Core.Text;
using System;

namespace Cadmus.Export.Filters;

/// <summary>
/// Appender text filter. This just appends the specified text.
/// <para>Tag: <c>it.vedph.text-filter.str.appender</c>.
/// Old tag: <c>it.vedph.renderer-filter.appender</c>.</para>
/// </summary>
[Tag("it.vedph.text-filter.str.appender")]
public sealed class AppenderTextFilter : TextFilter<string>,
    IConfigurable<AppenderRendererFilterOptions>
{
    private string? _text;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(AppenderRendererFilterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _text = options.Text;
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
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(_text))
            return text;

        return text + _text;
    }
}

/// <summary>
/// Options for <see cref="AppenderTextFilter"/>.
/// </summary>
public class AppenderRendererFilterOptions
{
    /// <summary>
    /// Gets or sets the text to be appended.
    /// </summary>
    public string? Text { get; set; }
}
