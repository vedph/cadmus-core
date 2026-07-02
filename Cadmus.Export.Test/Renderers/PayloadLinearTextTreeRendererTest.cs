using Cadmus.Core;
using Cadmus.Export.Filters;
using Cadmus.Export.Renderers;
using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using Fusi.Tools.Data;
using System.Collections.Generic;
using System;
using Xunit;
using Proteus.Rendering;
using System.Text.Json;
using Proteus.Rendering.Filters;

namespace Cadmus.Export.Test.Renderers;

public sealed class PayloadLinearTextTreeRendererTest
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
    public void Apply_WithLines_SingleLine_Ok()
    {
        (TreeNode<ExportedSegment>? tree, IItem _) = GetTreeAndItem();
        PayloadLinearTextTreeRenderer renderer = new();

        string? json = renderer.Render(tree, new CadmusRendererContext());

        Assert.NotNull(json);
        Assert.StartsWith("[[", json);

        JsonDocument doc = JsonDocument.Parse(json);

        // array includes a single item which is another array (row)
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        JsonElement row = doc.RootElement[0];
        Assert.Equal(JsonValueKind.Array, row.ValueKind);

        // row contains 3 items
        Assert.Equal(3, row.GetArrayLength());

        // item 0 = "illuc" with payload 0-4 for fragment ID 0
        JsonElement item = row[0];
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
        // Text
        Assert.Equal("illuc", item.GetProperty("Text").GetString());
        // Payloads[0]/Start, Payloads[0]/End
        JsonElement payload = item.GetProperty("Payloads")[0];
        Assert.Equal(0, payload.GetProperty("Start").GetInt32());
        Assert.Equal(4, payload.GetProperty("End").GetInt32());
        // Payloads[0]/FragmentIds
        Assert.Equal(1, payload.GetProperty("FragmentIds").GetArrayLength());
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.apparatus@0",
            payload.GetProperty("FragmentIds")[0].GetString());

        // item 1 = " unde negant redire " with payload 5-24 for no fragments
        item = row[1];
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
        // Text
        Assert.Equal(" unde negant redire ", item.GetProperty("Text").GetString());
        // Payloads[0]/Start, Payloads[0]/End
        payload = item.GetProperty("Payloads")[0];
        Assert.Equal(5, payload.GetProperty("Start").GetInt32());
        Assert.Equal(24, payload.GetProperty("End").GetInt32());
        Assert.Equal(0, payload.GetProperty("FragmentIds").GetArrayLength());

        // item 2 = "quemquam" with payload 25-32 for fragment ID 1
        item = row[2];
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
        // Text
        Assert.Equal("quemquam", item.GetProperty("Text").GetString());
        // Payloads[0]/Start, Payloads[0]/End
        payload = item.GetProperty("Payloads")[0];
        Assert.Equal(25, payload.GetProperty("Start").GetInt32());
        Assert.Equal(32, payload.GetProperty("End").GetInt32());
        // Payloads[0]/FragmentIds
        Assert.Equal(1, payload.GetProperty("FragmentIds").GetArrayLength());
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.apparatus@1",
            payload.GetProperty("FragmentIds")[0].GetString());
    }

    [Fact]
    public void Apply_WithoutLines_SingleLine_Ok()
    {
        (TreeNode<ExportedSegment>? tree, IItem _) = GetTreeAndItem();
        PayloadLinearTextTreeRenderer renderer = new();
        renderer.Configure(new PayloadLinearTextTreeRendererOptions
        {
            FlattenLines = true
        });

        string? json = renderer.Render(tree, new CadmusRendererContext());

        Assert.NotNull(json);
        Assert.StartsWith("[{", json);

        JsonDocument doc = JsonDocument.Parse(json);

        // array includes a single item which is an object
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        JsonElement row = doc.RootElement;

        // row contains 3 items
        Assert.Equal(3, row.GetArrayLength());

        // item 0 = "illuc" with payload 0-4 for fragment ID 0
        JsonElement item = row[0];
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
        // Text
        Assert.Equal("illuc", item.GetProperty("Text").GetString());
        // Payloads[0]/Start, Payloads[0]/End
        JsonElement payload = item.GetProperty("Payloads")[0];
        Assert.Equal(0, payload.GetProperty("Start").GetInt32());
        Assert.Equal(4, payload.GetProperty("End").GetInt32());
        // Payloads[0]/FragmentIds
        Assert.Equal(1, payload.GetProperty("FragmentIds").GetArrayLength());
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.apparatus@0",
            payload.GetProperty("FragmentIds")[0].GetString());

        // item 1 = " unde negant redire " with payload 5-24 for no fragments
        item = row[1];
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
        // Text
        Assert.Equal(" unde negant redire ", item.GetProperty("Text").GetString());
        // Payloads[0]/Start, Payloads[0]/End
        payload = item.GetProperty("Payloads")[0];
        Assert.Equal(5, payload.GetProperty("Start").GetInt32());
        Assert.Equal(24, payload.GetProperty("End").GetInt32());
        Assert.Equal(0, payload.GetProperty("FragmentIds").GetArrayLength());

        // item 2 = "quemquam" with payload 25-32 for fragment ID 1
        item = row[2];
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
        // Text
        Assert.Equal("quemquam", item.GetProperty("Text").GetString());
        // Payloads[0]/Start, Payloads[0]/End
        payload = item.GetProperty("Payloads")[0];
        Assert.Equal(25, payload.GetProperty("Start").GetInt32());
        Assert.Equal(32, payload.GetProperty("End").GetInt32());
        // Payloads[0]/FragmentIds
        Assert.Equal(1, payload.GetProperty("FragmentIds").GetArrayLength());
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.apparatus@1",
            payload.GetProperty("FragmentIds")[0].GetString());
    }

    // TODO: add multiple lines tests
}
