using Cadmus.Core;
using Cadmus.Export.ML.Renderers;
using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using Fusi.Tools.Data;
using System.Collections.Generic;
using System;
using Xunit;
using Proteus.Rendering;
using Proteus.Rendering.Filters;

namespace Cadmus.Export.ML.Test.Renderers;

public sealed class TeiAppLinearTextTreeRendererTest
{
    private static TokenTextPart GetTextPart()
    {
        TokenTextPart part = new();
        part.Lines.Add(new TextLine
        {
            Y = 1,
            Text = "illuc unde negant redire quemquam"
        });
        return part;
    }

    private static TokenTextLayerPart<ApparatusLayerFragment> GetApparatusPart()
    {
        // 1     2    3      4      5
        // illuc unde negant redire quemquam
        // AAAAA....................BBBBBBBB
        TokenTextLayerPart<ApparatusLayerFragment> part = new();

        // illuc
        part.Fragments.Add(new()
        {
            Location = "1.1",
            Entries =
            [
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Note,
                    IsAccepted = true,
                    Witnesses =
                    [
                        new AnnotatedValue
                        {
                            Value = "O1",
                        }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "illud",
                    Witnesses =
                    [
                        new AnnotatedValue { Value = "O" },
                        new AnnotatedValue { Value = "G" },
                        new AnnotatedValue { Value = "R" }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "illic",
                    Authors =
                    [
                        new LocAnnotatedValue
                        {
                            Value = "Fruterius",
                            Note = "(†1566) 1605a 388"
                        },
                    ]
                },
            ]
        });

        // quemquam
        part.Fragments.Add(new()
        {
            Location = "1.5",
            Entries =
            [
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Note,
                    IsAccepted = true,
                    Witnesses =
                    [
                        new AnnotatedValue { Value = "O" },
                        new AnnotatedValue { Value = "G" },
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "umquam",
                    Witnesses =
                    [
                        new AnnotatedValue { Value = "R" }
                    ],
                    Note = "some note"
                },
            ]
        });

        return part;
    }

    public static (TreeNode<ExportedSegment> tree, IItem item) GetTreeAndItem()
    {
        // get item
        TokenTextPart textPart = GetTextPart();
        TokenTextLayerPart<ApparatusLayerFragment> appPart = GetApparatusPart();
        Item item = new();
        item.Parts.Add(textPart);
        item.Parts.Add(appPart);

        // flatten
        TokenTextPartFlattener flattener = new();
        Tuple<string, IList<AnnotatedTextRange>> tr = flattener.Flatten(
            textPart, [appPart]);

        // merge ranges
        IList<AnnotatedTextRange> mergedRanges = AnnotatedTextRange.GetConsecutiveRanges(
            0, tr.Item1.Length - 1, tr.Item2);
        // assign text to merged ranges
        foreach (AnnotatedTextRange range in mergedRanges)
            range.AssignText(tr.Item1);

        // build a linear tree from ranges
        TreeNode<ExportedSegment> tree = CadmusTextTreeBuilder.BuildTreeFromRanges(
            mergedRanges, tr.Item1);
        // apply block filter
        return (new BlockLinearTextTreeFilter().Apply(tree, item), item);
    }

    [Fact]
    public void Render_NoItemSource_Ok()
    {
        TeiAppLinearTextTreeRenderer renderer = new();
        renderer.Configure(new AppLinearTextTreeRendererOptions
        {
            NoItemSource = true
        });

        (TreeNode<ExportedSegment>? tree, IItem item) = GetTreeAndItem();

        string? xml = renderer.Render(tree, new CadmusRendererContext
        {
            Source = item
        });

        Assert.Equal("<p n=\"1\" xmlns=\"http://www.tei-c.org/ns/1.0\"><app n=\"1\">" +
            "<lem n=\"1\" wit=\"#O1\">illuc</lem><rdg n=\"2\" wit=\"#O #G #R\">" +
            "illud</rdg><rdg n=\"3\" xml:id=\"rdg1\" resp=\"#Fruterius\">" +
            "illic</rdg><witDetail target=\"#rdg1\" resp=\"#Fruterius\">" +
            "(†1566) 1605a 388</witDetail></app> unde negant redire " +
            "<app n=\"2\"><lem n=\"1\" wit=\"#O #G\">quemquam</lem>" +
            "<rdg n=\"2\" wit=\"#R\">umquam<note>some note</note></rdg></app></p>",
            xml);
    }

    [Fact]
    public void Render_ItemSource_Ok()
    {
        TeiAppLinearTextTreeRenderer renderer = new();

        (TreeNode<ExportedSegment>? tree, IItem item) = GetTreeAndItem();

        string? xml = renderer.Render(tree, new CadmusRendererContext
        {
            Source = item
        });

        Assert.Equal($"<p source=\"^{item.Id}\" " +
            "n=\"1\" xmlns=\"http://www.tei-c.org/ns/1.0\"><app n=\"1\">" +
            "<lem n=\"1\" wit=\"#O1\">illuc</lem><rdg n=\"2\" wit=\"#O #G #R\">" +
            "illud</rdg><rdg n=\"3\" xml:id=\"rdg1\" resp=\"#Fruterius\">" +
            "illic</rdg><witDetail target=\"#rdg1\" resp=\"#Fruterius\">" +
            "(†1566) 1605a 388</witDetail></app> unde negant redire " +
            "<app n=\"2\"><lem n=\"1\" wit=\"#O #G\">quemquam</lem>" +
            "<rdg n=\"2\" wit=\"#R\">umquam<note>some note</note></rdg></app></p>",
            xml);
    }
}
