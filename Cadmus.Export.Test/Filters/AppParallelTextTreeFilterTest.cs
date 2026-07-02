using Cadmus.Core;
using Cadmus.Export.Filters;
using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using Fusi.Tools;
using Fusi.Tools.Data;
using Proteus.Rendering;
using Proteus.Rendering.Filters;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Cadmus.Export.Test.Filters;

public sealed class AppParallelTextTreeFilterTest
{
    private static TokenTextPart GetTextPart()
    {
        TokenTextPart part = new();
        part.Lines.Add(new TextLine
        {
            Y = 1,
            Text = "tecum ludere sicut ipsa possem"
        });
        return part;
    }

    private static TokenTextLayerPart<ApparatusLayerFragment> GetApparatusPart()
    {
        // 1     2      3     4    5
        // tecum ludere sicut ipsa possem
        // AAAAA.BBBBBB............CCCCCC
        TokenTextLayerPart<ApparatusLayerFragment> part = new();

        // tecum OGR: secum O1
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
                        new AnnotatedValue { Value = "O" },
                        new AnnotatedValue { Value = "G" },
                        new AnnotatedValue { Value = "R" }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "secum",
                    Witnesses =
                    [
                        new AnnotatedValue { Value = "O1" },
                    ]
                }
            ]
        });

        // ludere O1GR: luderem O | loedere Trappers-Lomax, 2007 69
        part.Fragments.Add(new ApparatusLayerFragment()
        {
            Location = "1.2",
            Entries =
            [
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Note,
                    IsAccepted = true,
                    Witnesses =
                    [
                        new AnnotatedValue { Value = "O1" },
                        new AnnotatedValue { Value = "G" },
                        new AnnotatedValue { Value = "R" }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "luderem",
                    Witnesses =
                    [
                        new AnnotatedValue { Value = "O" },
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "loedere",
                    Authors =
                    [
                        new LocAnnotatedValue
                        {
                            Value = "Trappers-Lomax",
                            Location= "2007 69"
                        },
                    ]
                }
            ]
        });

        // possem OGR: possum MS48 | possim Turnebus, 1573 26 |
        // posse Vossius, 1684 | posset Heinsius, dub. 1646-81
        part.Fragments.Add(new ApparatusLayerFragment()
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
                        new AnnotatedValue { Value = "R" }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "possum",
                    Witnesses =
                    [
                        new LocAnnotatedValue
                        {
                            Value = "MS48",
                        }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "possim",
                    Authors =
                    [
                        new LocAnnotatedValue
                        {
                            Value = "Turnebus",
                            Location = "1573 26"
                        }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "posse",
                    Authors =
                    [
                        new LocAnnotatedValue
                        {
                            Value = "Vossius",
                            Location = "1684"
                        }
                    ]
                },
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "posset",
                    Authors =
                    [
                        new LocAnnotatedValue
                        {
                            Value = "Heinsius",
                            Note = "dub.",
                            Location = "1646-81"
                        }
                    ]
                }
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

    private static void AssertContainsTags(IList<StringPair> features,
        string context, params string[] tags)
    {
        foreach (string tag in tags)
        {
            bool containsTag = features.Any(f => f.Name == "tag" && f.Value == tag);
            Assert.True(containsTag,
                $"Tag '{tag}' not found in context: {context}");
        }
    }

    [Fact]
    public void Apply_Binary_Ok()
    {
        (TreeNode<ExportedSegment> tree, IItem item) = GetTreeAndItem();
        AppParallelTextTreeFilter filter = new();
        filter.Configure(new AppParallelTextTreeFilterOptions { IsBinary = true });

        TreeNode<ExportedSegment> result = filter.Apply(tree, item);

        Assert.NotNull(result);

        // 1.1 root
        Assert.Null(result.Data);

        // 2.1 fork
        Assert.Single(result.Children);
        TreeNode<ExportedSegment> fork21 = result.Children[0];
        Assert.Null(fork21.Data);

        // 3.1 tecum
        Assert.Equal(2, fork21.Children.Count);
        TreeNode<ExportedSegment>? tecum = fork21.FirstChild;
        Assert.NotNull(tecum);
        Assert.Equal("tecum", tecum.Data?.Text);
        // 9 tags: empty, w:O, w:G, w:R,
        // a:Trappers-Lomax, w:MS48, a:Turnebus, a:Vossius, a:Heinsius
        AssertContainsTags(tecum.Data!.Features!, "tecum@3.1",
            "", "w:O", "w:G", "w:R",
            "a:Trappers-Lomax", "w:MS48", "a:Turnebus", "a:Vossius", "a:Heinsius");

        // 4.1 fork
        TreeNode<ExportedSegment>? fork41 = tecum.FirstChild;
        Assert.NotNull(fork41);
        Assert.NotNull(fork41.Data);
        // 9 tags: empty, w:O, w:G, w:R,
        // a:Trappers-Lomax, w:MS48, a:Turnebus, a:Vossius, a:Heinsius
        AssertContainsTags(fork41.Data.Features!, "fork@4.1",
            "", "w:O", "w:G", "w:R",
            "a:Trappers-Lomax", "w:MS48", "a:Turnebus", "a:Vossius", "a:Heinsius");

        // 5.1. fork
        TreeNode<ExportedSegment>? fork51 = fork41.FirstChild;
        Assert.NotNull(fork51);
        Assert.Null(fork51.Data);

        // 6.1. ludere
        TreeNode<ExportedSegment>? ludere = fork51.FirstChild;
        Assert.NotNull(ludere);
        Assert.Equal("ludere", ludere.Data?.Text);
        // 7 tags: empty, w:G, w:R, w:MS48, a:Turnebus, a:Vossius, a:Heinsius
        AssertContainsTags(ludere.Data!.Features!, "ludere@6.1",
            "", "w:G", "w:R", "w:MS48", "a:Turnebus", "a:Vossius", "a:Heinsius");

        // 7.1. sicut ipsa
        TreeNode<ExportedSegment>? sicutIpsa = ludere.FirstChild;
        Assert.NotNull(sicutIpsa);
        Assert.Equal(" sicut ipsa ", sicutIpsa.Data?.Text);
        // 7 tags: empty, w:G, w:R, w:MS48, a:Turnebus, a:Vossius, a:Heinsius
        AssertContainsTags(sicutIpsa.Data!.Features!, "sicut ipsa@7.1",
            "", "w:G", "w:R", "w:MS48", "a:Turnebus", "a:Vossius", "a:Heinsius");

        // 8.1 fork
        TreeNode<ExportedSegment>? fork81 = sicutIpsa.FirstChild;
        Assert.NotNull(fork81);
        Assert.Null(fork81.Data);

        // 9.1 possem
        TreeNode<ExportedSegment>? possem = fork81.FirstChild;
        Assert.NotNull(possem);
        Assert.Equal("possem", possem.Data?.Text);
        // 3 tags: empty, w:G, w:R
        AssertContainsTags(possem.Data!.Features!, "possem@9.1",
            "", "w:G", "w:R");
        // leaf
        Assert.False(possem.HasChildren);

        // 9.2 fork
        TreeNode<ExportedSegment>? fork92 = fork81.Children[1];
        Assert.NotNull(fork92);
        Assert.Null(fork92.Data);

        // 10.1 fork
        TreeNode<ExportedSegment>? fork101 = fork92.FirstChild;
        Assert.NotNull(fork101);
        Assert.Null(fork101.Data);

        // 11.1 fork
        TreeNode<ExportedSegment>? fork111 = fork101.FirstChild;
        Assert.NotNull(fork111);
        Assert.Null(fork111.Data);

        // 12.1 possum
        TreeNode<ExportedSegment>? possum = fork111.FirstChild;
        Assert.NotNull(possum);
        Assert.Equal("possum", possum.Data?.Text);
        // 1 tag: w:MS48
        AssertContainsTags(possum.Data!.Features!, "possum@12.1", "w:MS48");
        // leaf
        Assert.False(possum.HasChildren);

        // 12.2 possim
        TreeNode<ExportedSegment>? possim = fork111.Children[1];
        Assert.NotNull(possim);
        Assert.Equal("possim", possim.Data?.Text);
        // 1 tag: a:Turnebus
        AssertContainsTags(possim.Data!.Features!, "possim@12.2", "a:Turnebus");
        // leaf
        Assert.False(possim.HasChildren);

        // 11.2 posse
        TreeNode<ExportedSegment>? posse = fork101.Children[1];
        Assert.NotNull(posse);
        Assert.Equal("posse", posse.Data?.Text);
        // 1 tag: a:Vossius
        AssertContainsTags(posse.Data!.Features!, "posse@11.2", "a:Vossius");
        // leaf
        Assert.False(posse.HasChildren);

        // 10.2 posset
        TreeNode<ExportedSegment>? posset = fork92.Children[1];
        Assert.NotNull(posset);
        Assert.Equal("posset", posset.Data?.Text);
        // 1 tag: a:Heinsius
        AssertContainsTags(posset.Data!.Features!, "posset@10.2", "a:Heinsius");
        // leaf
        Assert.False(posset.HasChildren);

        // 6.2 fork
        TreeNode<ExportedSegment>? fork62 = fork51.Children[1];
        Assert.NotNull(fork62);
        Assert.Null(fork62.Data);

        // 7.1 luderem
        TreeNode<ExportedSegment>? luderem = fork62.FirstChild;
        Assert.NotNull(luderem);
        Assert.Equal("luderem", luderem.Data?.Text);
        // 1 tag: w:O
        AssertContainsTags(luderem.Data!.Features!, "luderem@7.1", "w:O");

        // 8.1 sicut ipsa
        TreeNode<ExportedSegment>? sicutIpsa81 = luderem.FirstChild;
        Assert.NotNull(sicutIpsa81);
        Assert.Equal(" sicut ipsa ", sicutIpsa81.Data?.Text);
        // 1 tag: w:O
        AssertContainsTags(sicutIpsa81.Data!.Features!, "sicut ipsa@8.1", "w:O");

        // 9.1 possem
        TreeNode<ExportedSegment>? possem91 = sicutIpsa81.FirstChild;
        Assert.NotNull(possem91);
        Assert.Equal("possem", possem91.Data?.Text);
        // 1 tag: w:O
        AssertContainsTags(possem91.Data!.Features!, "possem@9.1", "w:O");
        // leaf
        Assert.False(possem91.HasChildren);

        // 7.2 loedere
        TreeNode<ExportedSegment>? loedere = fork62.Children[1];
        Assert.NotNull(loedere);
        Assert.Equal("loedere", loedere.Data?.Text);
        // 1 tag: a:Trappers-Lomax
        AssertContainsTags(loedere.Data!.Features!, "loedere@7.2", "a:Trappers-Lomax");

        // 8.1 sicut ipsa
        TreeNode<ExportedSegment>? sicutIpsa82 = loedere.FirstChild;
        Assert.NotNull(sicutIpsa82);
        Assert.Equal(" sicut ipsa ", sicutIpsa82.Data?.Text);
        // 1 tag: a:Trappers-Lomax
        AssertContainsTags(sicutIpsa82.Data!.Features!, "sicut ipsa@8.1",
            "a:Trappers-Lomax");

        // 9.1 possem
        TreeNode<ExportedSegment>? possem91b = sicutIpsa82.FirstChild;
        Assert.NotNull(possem91b);
        Assert.Equal("possem", possem91b.Data?.Text);
        // 1 tag: a:Trappers-Lomax
        AssertContainsTags(possem91b.Data!.Features!, "possem@9.1",
            "a:Trappers-Lomax");

        // 3.2 secum
        TreeNode<ExportedSegment>? secum = fork21.Children[1];
        Assert.NotNull(secum);
        Assert.Equal("secum", secum.Data?.Text);
        // 1 tag: w:O1
        AssertContainsTags(secum.Data!.Features!, "secum@3.2", "w:O1");

        // 4.1 space
        TreeNode<ExportedSegment>? space = secum.FirstChild;
        Assert.NotNull(space);
        Assert.Equal(" ", space.Data?.Text);
        // 1 tag: w:O1
        AssertContainsTags(space.Data!.Features!, "space@4.1", "w:O1");

        // 5.1 ludere
        TreeNode<ExportedSegment>? ludere51 = space.FirstChild;
        Assert.NotNull(ludere51);
        Assert.Equal("ludere", ludere51.Data?.Text);
        // 1 tag: w:O1
        AssertContainsTags(ludere51.Data!.Features!, "ludere@5.1", "w:O1");

        // 6.1 sicut ipsa
        TreeNode<ExportedSegment>? sicutIpsa61 = ludere51.FirstChild;
        Assert.NotNull(sicutIpsa61);
        Assert.Equal(" sicut ipsa ", sicutIpsa61.Data?.Text);
        // 1 tag: w:O1
        AssertContainsTags(sicutIpsa61.Data!.Features!, "sicut ipsa@6.1", "w:O1");

        // 7.1 possem
        TreeNode<ExportedSegment>? possem71 = sicutIpsa61.FirstChild;
        Assert.NotNull(possem71);
        Assert.Equal("possem", possem71.Data?.Text);
        // 1 tag: w:O1
        AssertContainsTags(possem71.Data!.Features!, "possem@7.1", "w:O1");
        // leaf
        Assert.False(possem71.HasChildren);
    }
}
