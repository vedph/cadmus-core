using Fusi.Tools;
using Proteus.Core.Text;
using Proteus.Text.Plugs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Cadmus.Export.Renderers;

/// <summary>
/// Base class for renderers applying text filters to their result.
/// </summary>
public abstract class FilteredRenderer
{
    private readonly TextFilterAdapter _adapter;

    /// <summary>
    /// Gets the optional filters to apply after the renderer completes.
    /// </summary>
    public List<ITextFilter> Filters { get; init; } = [];

    /// <summary>
    /// Constructs a new instance of <see cref="CadmusJsonRenderer"/>.
    /// </summary>
    protected FilteredRenderer()
    {
        _adapter = new(
            [
                new StringTextFilterPlug(),
                new StringBuilderTextFilterPlug(),
                new XElementTextFilterPlug(),
            ]);
    }

    /// <summary>
    /// Applies the filters to the specified source object, returning a string
    /// with the result.
    /// </summary>
    /// <param name="source">The source object.</param>
    /// <param name="context">The optional rendering context.</param>
    /// <returns>String.</returns>
    public string ApplyFilters(object source,
        IHasDataDictionary? context = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        object? result = source;

        if (Filters.Count > 0)
        {
            foreach (ITextFilter filter in Filters.Where(f => !f.IsDisabled))
                result = filter.Apply(result, context);
        }

        return (string)(_adapter.Adapt(result, typeof(string), false) ?? "");
    }
}
