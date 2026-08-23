using Fluid;
using Fluid.Values;
using Fusi.Antiquity.Chronology;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cadmus.Export.Json.Filters;

/// <summary>
/// A Fluid filter to format a <see cref="HistoricalDate"/>. The input value is
/// expected to be a JSON representation of a <see cref="HistoricalDate"/>.
/// </summary>
[Tag("fluid-filter.historical-date")]
public sealed class HistoricalDateFluidFilter : IFluidFilter
{
    private static readonly JsonSerializerOptions _options = new()
    {
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };

    private static HistoricalDate? ParseDate(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<HistoricalDate>(json, _options);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.ToString());
            return null;
        }
    }

    /// <summary>
    /// Applies the filter to the specified input value, with the given
    /// arguments and context.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="arguments">The filter arguments. Pass "text" to get the
    /// textual representation; else it returns the numeric sort value.</param>
    /// <param name="context">The template context.</param>
    /// <returns>The filtered value.</returns>
    public ValueTask<FluidValue> Apply(FluidValue input,
        FilterArguments arguments, TemplateContext context)
    {
        // argument 0 is "text" for text, else it means "value"
        bool text = arguments.Count > 0 &&
            arguments.At(0).ToStringValue() == "text";

        // parse HistoricalDate
        HistoricalDate? date = ParseDate(input.ToStringValue());
        if (date is null) return new ValueTask<FluidValue>(NilValue.Instance);

        // return text or value
        if (text)
        {
            return new ValueTask<FluidValue>(new StringValue(
                date?.ToString() ?? ""));
        }
        else
        {
            return new ValueTask<FluidValue>(NumberValue.Create(
                (decimal)(date?.GetSortValue() ?? 0)));
        }
    }
}
