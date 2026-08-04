using Fluid;
using Fluid.Values;
using System.Threading.Tasks;

namespace Cadmus.Export.Json;

/// <summary>
/// A Fluid filter to be used in the <see cref="JsonTemplateNodeMapper"/>.
/// </summary>
public interface IFluidFilter
{
    /// <summary>
    /// Applies the filter to the specified input value, with the given
    /// arguments and context.
    /// </summary>
    /// <param name="input">The input value.</param>
    /// <param name="arguments">The filter arguments.</param>
    /// <param name="context">The template context.</param>
    /// <returns>The filtered value.</returns>
    ValueTask<FluidValue> Apply(FluidValue input, FilterArguments arguments,
        TemplateContext context);
}
