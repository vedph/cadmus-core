using Cadmus.Core;
using Cadmus.Export.Filters;
using Cadmus.Export.ML.Renderers;
using Cadmus.Export.Test.Renderers;
using Fusi.Tools.Data;
using Proteus.Rendering;
using Xunit;

namespace Cadmus.Export.ML.Test.Renderers;

public sealed class TeiAppParallelTextTreeRendererTest
{
    [Fact]
    public void Render_Ok()
    {
        // flattened tree:
        // + ⯈ [1.1] #4
        //  + ⯈ [2.1] illuc #1 → illuc
        //   + ⯈ [3.1]  unde negant redire  #2 →  unde negant redire 
        //    - ■ [4.1] quemquam #3 → quemquam
        (TreeNode<ExportedSegment>? tree, IItem item) =
            PayloadLinearTextTreeRendererTest.GetTreeAndItem();

        // n-ary tree:
        // + ⯈ [1.1]
        //  + ⯈ [2.1] illuc #1 → illuc F2: tag=, tag=w:O1
        //   + ⯈ [3.1]  unde negant redire  #2 →  unde negant redire  F2: tag=, tag=w:O1
        //    - ■ [4.1] quemquam #3 → quemquam F2: tag=, tag=w:O1
        //  + ⯈ [2.2] illud #1 → illud F3: tag=w:O, tag=w:G, tag=w:R
        //   + ⯈ [3.1]  unde negant redire  #2 →  unde negant redire  F3: tag=w:O, tag=w:G, tag=w:R
        //    - ■ [4.1] quemquam #3 → quemquam F2: tag=w:O, tag=w:G
        //    - ■ [4.2] umquam #3 → umquam F1: tag=w:R
        //  + ⯈ [2.3] illic #1 → illic F1: tag=a:Fruterius
        //   + ⯈ [3.1]  unde negant redire  #2 →  unde negant redire  F1: tag=a:Fruterius
        //    - ■ [4.1] quemquam #3 → quemquam F1: tag=a:Fruterius
        tree = new AppParallelTextTreeFilter().Apply(tree, item);

        TeiAppParallelTextTreeRenderer renderer = new();
        renderer.Configure(new TeiAppParallelTextTreeRendererOptions
        {
            NoItemSource = true
        });

        // act
        string? xml = renderer.Render(tree, new CadmusRendererContext
        {
            Source = item
        });

        Assert.Equal("<p n=\"1\" xmlns=\"http://www.tei-c.org/ns/1.0\">" +
            "<app>" +
              "<lem>illuc</lem>" +
              "<rdg>illud</rdg>" +
              "<rdg>illic</rdg>" +
            "</app>" +
            " unde negant redire " +
            "<app>" +
              "<rdg>quemquam</rdg>" +
              "<rdg>umquam</rdg>" +
            "</app></p>",
            xml);
    }
}
