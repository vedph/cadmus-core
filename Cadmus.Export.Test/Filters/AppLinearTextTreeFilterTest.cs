/*
using Cadmus.Core;
using Cadmus.Export.Filters;
using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using Fusi.Tools.Data;
using System;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class AppLinearTextTreeFilterTest
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

    public static (TreeNode<TextSpanPayload> tree, IItem item) GetTreeAndItem()
    {
        // get item
        TokenTextPart textPart = GetTextPart();
        TokenTextLayerPart<ApparatusLayerFragment> appPart = GetApparatusPart();
        IItem item = new Item();
        item.Parts.Add(textPart);
        item.Parts.Add(appPart);

        // flatten
        TokenTextPartFlattener flattener = new();
        Tuple<string, IList<FragmentTextRange>> tr = flattener.Flatten(
            textPart, [appPart]);

        // merge ranges
        IList<FragmentTextRange> mergedRanges = FragmentTextRange.MergeRanges(
            0, tr.Item1.Length - 1, tr.Item2);
        // assign text to merged ranges
        foreach (FragmentTextRange range in mergedRanges)
            range.AssignText(tr.Item1);

        // build a linear tree from ranges
        TreeNode<TextSpanPayload> tree = ItemComposer.BuildTreeFromRanges(
            mergedRanges, tr.Item1);
        // apply block filter
        return (new BlockLinearTextTreeFilter().Apply(tree, item), item);
    }

    [Fact]
    public void Apply_Ok()
    {
        (TreeNode<TextSpanPayload>? tree, IItem item) = GetTreeAndItem();

        // act
        AppLinearTextTreeFilter filter = new();
        filter.Apply(tree, item);

        // first node is blank root
        Assert.Null(tree.Data);

        // next child is illuc
        Assert.Single(tree.Children);
        TreeNode<TextSpanPayload> node = tree.Children[0];
        Assert.NotNull(node.Data);
        Assert.Equal("illuc", node.Data.Text);

        // illuc has 3 sets
        Assert.Equal(3, node.Data.FeatureSets.Count);

        // from entry 0: app.e.witness=O1
        TextSpanFeatureSet set = node.Data.FeatureSets["e000"];
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[0].Name);
        Assert.Equal("O1", set.Features[0].Value);

        // from entry 1:
        set = node.Data.FeatureSets["e001"];
        // - app.e.variant=illud
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_VARIANT,
            set.Features[0].Name);
        Assert.Equal("illud", set.Features[0].Value);

        // - app-witness=O,G,R
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[1].Name);
        Assert.Equal("O", set.Features[1].Value);

        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[2].Name);
        Assert.Equal("G", set.Features[2].Value);

        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[3].Name);
        Assert.Equal("R", set.Features[3].Value);

        // from entry 2:
        set = node.Data.FeatureSets["e002"];
        // - app-variant=illic
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_VARIANT,
            set.Features[0].Name);
        Assert.Equal("illic", set.Features[0].Value);

        // - app-author=Fruterius
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_AUTHOR,
            set.Features[1].Name);
        Assert.Equal("Fruterius", set.Features[1].Value);

        // - app-author.note=(†1566) 1605a 388
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_AUTHOR_NOTE,
            set.Features[2].Name);
        Assert.Equal("(†1566) 1605a 388", set.Features[2].Value);

        // next child is unde negant redire
        Assert.Single(node.Children);
        node = node.Children[0];
        Assert.NotNull(node.Data);
        Assert.Equal(" unde negant redire ", node.Data.Text);
        Assert.Empty(node.Data!.FeatureSets);

        // next child is quemquam
        Assert.Single(node.Children);
        node = node.Children[0];
        Assert.NotNull(node.Data);
        Assert.Equal("quemquam", node.Data.Text);

        // from entry 0:
        set = node.Data.FeatureSets["e000"];
        // - app-witness=O,G
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[0].Name);
        Assert.Equal("O", set.Features[0].Value);

        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[1].Name);
        Assert.Equal("G", set.Features[1].Value);

        // from entry 1:
        set = node.Data.FeatureSets["e001"];
        // - app-variant=umquam
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_VARIANT,
            set.Features[0].Name);
        Assert.Equal("umquam", set.Features[0].Value);

        // - app-witness=R
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_WITNESS,
            set.Features[1].Name);
        Assert.Equal("R", set.Features[1].Value);

        // - app-note=some note
        Assert.Equal(AppLinearTextTreeFilter.F_APP_E_NOTE,
            set.Features[2].Name);
        Assert.Equal("some note", set.Features[2].Value);

        // no more children
        Assert.Empty(node.Children);
    }
}
*/