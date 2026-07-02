namespace Proteus.Rendering;

/// <summary>
/// Options for a composite component which wraps an inner component of the
/// same type.
/// </summary>
/// <typeparam name="TInnerOptions">The type of the inner component's options
/// </typeparam>
public class CompositeOptions<TInnerOptions> where TInnerOptions : class, new()
{
    /// <summary>
    /// The options of the inner wrapped component.
    /// </summary>
    public TInnerOptions? InnerOptions { get; set; }
}
