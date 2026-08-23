using Fluid;
using Fluid.Values;
using Fusi.Antiquity.Chronology;
using Fusi.Tools.Configuration;
using System.Threading.Tasks;

namespace Cadmus.Export.Json.Filters;

/// <summary>
/// A Fluid filter to format a <see cref="HistoricalDate"/>. The input value is
/// expected to be a JSON representation of a <see cref="HistoricalDate"/>.
/// <para>Tag: <c>fluid-filter.historical-date</c>.</para>
/// </summary>
[Tag("fluid-filter.historical-date")]
public sealed class HistoricalDateFluidFilter : IFluidFilter
{
    /// <summary>
    /// Parses the specified Fluid value into a <see cref="HistoricalDate"/>.
    /// If the value is a string, it is expected to be a string representation
    /// of <see cref="HistoricalDate"/>; if it is an object, it is expected
    /// to be a dictionary object representing the structure of a
    /// <see cref="HistoricalDate"/> as derived from its JSON serialization.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <returns>The parsed <see cref="HistoricalDate"/>, or <c>null</c> if
    /// parsing fails.  </returns>
    private static HistoricalDate? ParseDate(FluidValue value)
    {
        if (value is StringValue str)
        {
            return HistoricalDate.Parse(str.ToStringValue()) ?? null;
        }
        else if (value is ObjectValue obj)
        {
            // TODO parse dictionary into HistoricalDate
        }
        return null;
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
        HistoricalDate? date = ParseDate(input);
        if (date is null)
            return new ValueTask<FluidValue>(NilValue.Instance);

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
