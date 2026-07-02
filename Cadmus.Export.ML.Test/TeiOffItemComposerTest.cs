using Cadmus.Core;
using Cadmus.Export.ML.Renderers;
using Cadmus.Export.Renderers;
using Cadmus.General.Parts;
using Cadmus.Philology.Parts;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Export.ML.Test;

public sealed class TeiOffItemComposerTest
{
    private static Item GetItem()
    {
        Item item = new()
        {
            Title = "carmen II",
            CreatorId = "zeus",
            UserId = "zeus",
            Description = "About a dead sparrow.",
            FacetId = "text",
            Flags = 1,
            GroupId = "carm-002",
            SortKey = "002"
        };

        // text
        TokenTextPart textPart = new()
        {
            ItemId = item.Id,
            CreatorId = item.CreatorId,
            UserId = item.UserId,
            RoleId = PartBase.BASE_TEXT_ROLE_ID,
            Citation = "Catull.2,1-2",
        };
        textPart.Lines.Add(new TextLine
        {
            Y = 1,
            Text = "Passer, deliciae meae puellae,"
        });
        textPart.Lines.Add(new TextLine
        {
            Y = 2,
            Text = "quicum ludere, quem in sinu tenere,"
        });
        item.Parts.Add(textPart);

        // apparatus layer
        TokenTextLayerPart<ApparatusLayerFragment> appPart = new()
        {
            ItemId = item.Id,
            CreatorId = item.CreatorId,
            UserId = item.UserId,
        };
        appPart.Fragments.Add(new ApparatusLayerFragment
        {
            Location = "1.2",
            Entries = [.. new[]
            {
                new ApparatusEntry
                {
                    Type = ApparatusEntryType.Replacement,
                    Value = "delicium",
                    Note = "teste Statio 1566"
                }
            }]
        });
        item.Parts.Add(appPart);

        // comment layer
        TokenTextLayerPart<CommentLayerFragment> commPart = new()
        {
            ItemId = item.Id,
            CreatorId = item.CreatorId,
            UserId = item.UserId,
        };
        commPart.Fragments.Add(new CommentLayerFragment
        {
            Location = "1.1",
            Text = "A sparrow."
        });
        item.Parts.Add(commPart);

        return item;
    }

    private static RamTeiOffItemComposer GetComposer()
    {
        RamTeiOffItemComposer composer = new()
        {
            TextPartFlattener = new TokenTextPartFlattener(),
            TextTreeRenderer = new TeiOffLinearTextTreeRenderer()
        };

        composer.JsonRenderers["it.vedph.token-text"] = new NullJsonRenderer();
        composer.JsonRenderers["it.vedph.token-text-layer:fr.it.vedph.apparatus"]
            = new NullJsonRenderer();
        composer.JsonRenderers["it.vedph.token-text-layer:fr.it.vedph.comment"]
            = new NullJsonRenderer();

        return composer;
    }

    [Fact]
    public void Compose_Ok()
    {
        RamTeiOffItemComposer composer = GetComposer();

        composer.Open();
        composer.Compose(GetItem());

        IDictionary<string, string> flows = composer.GetFlows();
        Assert.Equal(3, flows.Count);

        // TODO
    }
}
