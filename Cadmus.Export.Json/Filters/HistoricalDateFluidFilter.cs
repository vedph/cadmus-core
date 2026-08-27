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
    /// Reads a <see cref="Datation"/> from the specified Fluid value, using
    /// Fluid's own member access API (<see cref="FluidValue.GetValueAsync"/>)
    /// rather than trying to unwrap the value's backing store: this way the
    /// same code works no matter how Fluid represents the object internally
    /// (e.g. a <see cref="DictionaryValue"/> built from a
    /// <c>Dictionary&lt;string, object&gt;</c>, or an <see cref="ObjectValue"/>
    /// built from a POCO).
    /// </summary>
    private static async ValueTask<Datation?> ParseDatationAsync(
        FluidValue value, TemplateContext context)
    {
        if (value is null || value.IsNil()) return null;

        Datation datation = new();

        FluidValue v = await value.GetValueAsync("value", context);
        if (!v.IsNil()) datation.Value = (int)v.ToNumberValue();

        FluidValue isCentury = await value.GetValueAsync("isCentury", context);
        if (!isCentury.IsNil()) datation.IsCentury = isCentury.ToBooleanValue();

        FluidValue isSpan = await value.GetValueAsync("isSpan", context);
        if (!isSpan.IsNil()) datation.IsSpan = isSpan.ToBooleanValue();

        FluidValue isApproximate = await value.GetValueAsync(
            "isApproximate", context);
        if (!isApproximate.IsNil())
            datation.IsApproximate = isApproximate.ToBooleanValue();

        FluidValue isDubious = await value.GetValueAsync("isDubious", context);
        if (!isDubious.IsNil()) datation.IsDubious = isDubious.ToBooleanValue();

        FluidValue day = await value.GetValueAsync("day", context);
        if (!day.IsNil()) datation.Day = (short)day.ToNumberValue();

        FluidValue month = await value.GetValueAsync("month", context);
        if (!month.IsNil()) datation.Month = (short)month.ToNumberValue();

        FluidValue hint = await value.GetValueAsync("hint", context);
        if (!hint.IsNil()) datation.Hint = hint.ToStringValue();

        FluidValue slide = await value.GetValueAsync("slide", context);
        if (!slide.IsNil()) datation.Slide = (int)slide.ToNumberValue();

        return datation;
    }

    /// <summary>
    /// Parses the specified Fluid value into a <see cref="HistoricalDate"/>.
    /// If the value is a string, it is expected to be a string representation
    /// of <see cref="HistoricalDate"/>; if it is an object, it is expected
    /// to be a dictionary object representing the structure of a
    /// <see cref="HistoricalDate"/> as derived from its JSON serialization.
    /// </summary>
    /// <param name="value">The value to parse.</param>
    /// <param name="context">The template context, used to resolve member
    /// access on <paramref name="value"/>.</param>
    /// <returns>The parsed <see cref="HistoricalDate"/>, or <c>null</c> if
    /// parsing fails.</returns>
    private static async ValueTask<HistoricalDate?> ParseDateAsync(
        FluidValue value, TemplateContext context)
    {
        if (value is StringValue str)
            return HistoricalDate.Parse(str.ToStringValue());

        if (value is null || value.IsNil()) return null;

        HistoricalDate date = new();

        FluidValue a = await value.GetValueAsync("a", context);
        Datation? datationA = await ParseDatationAsync(a, context);
        if (datationA != null) date.A = datationA;

        FluidValue b = await value.GetValueAsync("b", context);
        Datation? datationB = await ParseDatationAsync(b, context);
        if (datationB != null) date.B = datationB;

        return date;
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
    public async ValueTask<FluidValue> Apply(FluidValue input,
        FilterArguments arguments, TemplateContext context)
    {
        // argument 0 is "text" for text, else it means "value"
        bool text = arguments.Count > 0 &&
            arguments.At(0).ToStringValue() == "text";

        // parse HistoricalDate
        HistoricalDate? date = await ParseDateAsync(input, context);
        if (date is null) return NilValue.Instance;

        // return text or value
        if (text) return new StringValue(date.ToString() ?? "");

        return NumberValue.Create((decimal)date.GetSortValue());
    }
}
