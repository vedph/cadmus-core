using Fluid;
using Fluid.Values;
using Fusi.Antiquity.Chronology;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Text.Json;
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
    /// Converts a dictionary to a Datation object by extracting its properties.
    /// </summary>
    private static Datation? DictionaryToDatation(object? obj)
    {
        if (obj == null) return null;

        if (obj is not IDictionary<string, object> dict) return null;

        Datation datation = new();

        // Extract properties (case-insensitive)
        Dictionary<string, object> dictLower =
            new(dict, StringComparer.OrdinalIgnoreCase);

        if (dictLower.TryGetValue("value", out var valueObj) &&
            valueObj is not null)
        {
            datation.Value = Convert.ToInt32(valueObj);
        }

        if (dictLower.TryGetValue("iscentury", out var isCenturyObj) &&
            isCenturyObj is not null)
        {
            datation.IsCentury = Convert.ToBoolean(isCenturyObj);
        }

        if (dictLower.TryGetValue("isspan", out var isSpanObj) &&
            isSpanObj is not null)
        {
            datation.IsSpan = Convert.ToBoolean(isSpanObj);
        }

        if (dictLower.TryGetValue("isapproximate", out var isApproximateObj) &&
            isApproximateObj is not null)
        {
            datation.IsApproximate = Convert.ToBoolean(isApproximateObj);
        }

        if (dictLower.TryGetValue("isdubious", out var isDubiousObj) &&
            isDubiousObj is not null)
        {
            datation.IsDubious = Convert.ToBoolean(isDubiousObj);
        }

        if (dictLower.TryGetValue("day", out var dayObj) && dayObj is not null)
        {
            datation.Day = Convert.ToInt16(dayObj);
        }

        if (dictLower.TryGetValue("month", out var monthObj) && monthObj is not null)
        {
            datation.Month = Convert.ToInt16(monthObj);
        }

        if (dictLower.TryGetValue("hint", out var hintObj) && hintObj is not null)
        {
            datation.Hint = hintObj.ToString();
        }

        if (dictLower.TryGetValue("slide", out var slideObj) && slideObj is not null)
        {
            datation.Slide = Convert.ToInt32(slideObj);
        }

        return datation;
    }

    /// <summary>
    /// Converts a Fluid value representing a Datation to a Datation object.
    /// </summary>
    private static Datation? FluidValueToDatation(FluidValue value)
    {
        if (value is DictionaryValue dictValue)
        {
            // Extract the underlying dictionary using reflection
            var underlying = dictValue.ToObjectValue();
            if (underlying is IDictionary<string, object> dict)
            {
                return DictionaryToDatation(underlying);
            }

            // If ToObjectValue() returned something else, try to convert it to
            // JSON and deserialize to handle the ObjectDictionaryFluidIndexable case
            try
            {
                string json = JsonSerializer.Serialize(underlying);
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };
                using JsonDocument doc = JsonDocument.Parse(json);
                JsonElement root = doc.RootElement;
                Dictionary<string, object> tempDict = new();
                foreach (var prop in root.EnumerateObject())
                {
                    tempDict[prop.Name] = prop.Value.GetRawText();
                }
                return DictionaryToDatation(tempDict);
            }
            catch
            {
                return null;
            }
        }
        else if (value is ObjectValue objValue)
        {
            var underlying = objValue.ToObjectValue();
            if (underlying is IDictionary<string, object> dict)
            {
                return DictionaryToDatation(underlying);
            }
        }

        return null;
    }

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
        else if (value is DictionaryValue || value is ObjectValue)
        {
            // extract the underlying object from the Fluid value
            var underlying = (value as ObjectValue)?.ToObjectValue() ?? 
                           (value as DictionaryValue)?.ToObjectValue();

            if (underlying is IDictionary<string, object> dict)
            {
                HistoricalDate historicalDate = new();

                if (dict.TryGetValue("a", out var aObj))
                {
                    var datationA = DictionaryToDatation(aObj);
                    if (datationA != null)
                    {
                        historicalDate.A = datationA;
                    }
                }

                if (dict.TryGetValue("b", out var bObj))
                {
                    var datationB = DictionaryToDatation(bObj);
                    if (datationB != null)
                    {
                        historicalDate.B = datationB;
                    }
                }

                return historicalDate;
            }

            // if it's not a plain dictionary, serialize to JSON and back
            try
            {
                string json = JsonSerializer.Serialize(underlying);
                JsonSerializerOptions options = new()
                {
                    PropertyNameCaseInsensitive = true
                };
                return JsonSerializer.Deserialize<HistoricalDate>(json, options);
            }
            catch
            {
                return null;
            }
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
