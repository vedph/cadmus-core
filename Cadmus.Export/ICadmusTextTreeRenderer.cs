using Cadmus.Core;
using Proteus.Core.Text;
using Proteus.Rendering;
using System.Collections.Generic;

namespace Cadmus.Export;

/// <summary>
/// Cadmus renderer for text trees.
/// </summary>
public interface ICadmusTextTreeRenderer : ITextTreeRenderer<string>
{
    /// <summary>
    /// Text filters.
    /// </summary>
    List<ITextFilter> Filters { get; }

    //void OnGroupChanged(IItem item, string? prevGroupId, CadmusRendererContext context);
}
