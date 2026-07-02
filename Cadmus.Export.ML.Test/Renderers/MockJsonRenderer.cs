using Fusi.Tools.Data;
using Proteus.Rendering;
using System;

namespace Cadmus.Export.ML.Test.Renderers;

internal sealed class MockJsonRenderer(Func<string, IRendererContext,
    TreeNode<ExportedSegment>?, string> renderFunc) :
    Export.Renderers.CadmusJsonRenderer, IJsonRenderer
{
    private readonly Func<string, CadmusRendererContext,
        TreeNode<ExportedSegment>?, string> _renderFunc =
        renderFunc ?? throw new ArgumentNullException(nameof(renderFunc));

    protected override string DoRender(string json, CadmusRendererContext context,
        TreeNode<ExportedSegment>? tree = null) => _renderFunc(json, context, tree);
}
