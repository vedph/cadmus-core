namespace Proteus.Rendering;

/// <summary>
/// Interface implemented by composite components which wrap an inner
/// component of the same type.
/// </summary>
public interface IHasCompositeComponent<TComponent> where TComponent : class
{
    /// <summary>
    /// Gets or sets the inner component.
    /// </summary>
    TComponent? Component { get; set; }
}
