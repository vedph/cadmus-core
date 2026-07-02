using Cadmus.Core;
using Cadmus.Core.Config;
using Cadmus.Export.Config;
using Cadmus.General.Parts;
using Cadmus.Mongo;
using Cadmus.Philology.Parts;
using Cadmus.Refs.Bricks;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using Proteus.Rendering;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

namespace Cadmus.Export.Test;

[Collection(nameof(NonParallelResourceCollection))]
public sealed class CadmusPreviewerTest
{
    private const string DB_NAME = "cadmus-test";
    private const string ITEM_ID = "ccc23d28-d10a-4fe3-b1aa-9907679c881f";
    private const string TEXT_ID = "9a801c84-0c93-4074-b071-9f4f9885ba66";
    private const string ORTH_ID = "c99072ea-c488-484b-ac37-e22027039dc0";
    private const string COMM_ID = "b7bc0fec-4a69-42d1-835b-862330c6e7fa";

    private readonly MongoClient _client;
    private readonly JsonSerializerOptions _jsonOptions;

    public CadmusPreviewerTest()
    {
        _client = new MongoClient();
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private MongoPart CreateMongoPart(IPart part)
    {
        string content =
            JsonSerializer.Serialize(part, part.GetType(), _jsonOptions);

        return new MongoPart(part)
        {
            Content = BsonDocument.Parse(content)
        };
    }

    internal static TokenTextPart GetSampleTextPart()
    {
        // text
        TokenTextPart text = new()
        {
            Id = TEXT_ID,
            ItemId = ITEM_ID,
            CreatorId = "zeus",
            UserId = "zeus",
            Citation = "CIL 1,23",
        };
        text.Lines.Add(new TextLine
        {
            Y = 1,
            Text = "que bixit"
        });
        text.Lines.Add(new TextLine
        {
            Y = 2,
            Text = "annos XX"
        });
        return text;
    }

    private void SeedData(IMongoDatabase db)
    {
        // item
        IMongoCollection<MongoItem> items = db.GetCollection<MongoItem>
            (MongoItem.COLLECTION);
        items.InsertOne(new MongoItem
        {
            Id = ITEM_ID,
            FacetId = "default",
            Flags = 2,
            Title = "Sample",
            Description = "Sample",
            GroupId = "group",
            SortKey = "sample",
            CreatorId = "zeus",
            UserId = "zeus"
        });

        // parts
        IMongoCollection<MongoPart> parts = db.GetCollection<MongoPart>
            (MongoPart.COLLECTION);

        // 0123456789-1234567
        // que bixit|annos XX
        // ..O............... 1.1@3
        // ....O............. 1.2@1
        // ....CCCCCCCCCCC... 1.2-2.1
        // ................CC 2.2

        // text
        TokenTextPart text = GetSampleTextPart();
        parts.InsertOne(CreateMongoPart(text));

        // orthography
        TokenTextLayerPart<OrthographyLayerFragment> orthLayer = new()
        {
            Id = ORTH_ID,
            ItemId = ITEM_ID,
            CreatorId = "zeus",
            UserId = "zeus"
        };
        // qu[e]
        orthLayer.Fragments.Add(new OrthographyLayerFragment
        {
            Location = "1.1@3",
            Reference = "ae"
        });
        // [b]ixit
        orthLayer.Fragments.Add(new OrthographyLayerFragment
        {
            Location = "1.2@1",
            Reference = "v"
        });
        parts.InsertOne(CreateMongoPart(orthLayer));

        // comment
        TokenTextLayerPart<CommentLayerFragment> commLayer = new()
        {
            Id = COMM_ID,
            ItemId = ITEM_ID,
            CreatorId = "zeus",
            UserId = "zeus"
        };
        // bixit annos
        commLayer.AddFragment(new CommentLayerFragment
        {
            Location = "1.2-2.1",
            Text = "acc. rather than abl. is rarer but attested.",
            References =
            [
                new DocReference
                {
                    Citation = "Sandys 1927 63",
                    Tag = "m",
                    Type = "book"
                }
            ]
        });
        // XX
        commLayer.AddFragment(new CommentLayerFragment
        {
            Location = "2.2",
            Text = "for those morons not knowing this, it's 20."
        });
        parts.InsertOne(CreateMongoPart(commLayer));
    }

    private void InitDatabase()
    {
        // camel case everything:
        // https://stackoverflow.com/questions/19521626/mongodb-convention-packs/19521784#19521784
        ConventionPack pack =
        [
            new CamelCaseElementNameConvention()
        ];
        ConventionRegistry.Register("camel case", pack, _ => true);

        _client.DropDatabase(DB_NAME);
        IMongoDatabase db = _client.GetDatabase(DB_NAME);

        SeedData(db);
    }

    private static MongoCadmusRepository GetRepository()
    {
        TagAttributeToTypeMap map = new();
        map.Add(
        [
            typeof(NotePart).Assembly,
            typeof(ApparatusLayerFragment).Assembly
        ]);
        MongoCadmusRepository repository = new(
            new StandardPartTypeProvider(map),
            new StandardItemSortKeyBuilder());
        repository.Configure(new MongoCadmusRepositoryOptions
        {
            // use the default ConnectionStringTemplate (local DB)
            ConnectionString = "mongodb://localhost:27017/" + DB_NAME
        });
        return repository;
    }

    private static CadmusPreviewer GetPreviewer(MongoCadmusRepository repository)
    {
        CadmusRenderingFactory factory = TestHelper.GetFactory();
        return new(factory, repository);
    }

    [Fact]
    public void RenderPart_NullWithText_Ok()
    {
        InitDatabase();
        MongoCadmusRepository repository = GetRepository();
        CadmusPreviewer previewer = GetPreviewer(repository);

        string json = previewer.RenderPart(ITEM_ID, TEXT_ID);

        string? json2 = repository.GetPartContent(TEXT_ID);
        Assert.Equal(json, json2);
    }

    private static JsonElement? GetFragmentAt(JsonElement fragments, int index)
    {
        if (index >= fragments.GetArrayLength()) return null;

        int i = 0;
        foreach (JsonElement fr in fragments.EnumerateArray())
        {
            if (i == index) return fr;
            i++;
        }
        return null;
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void RenderFragment_NullWithOrth_Ok(int index)
    {
        InitDatabase();
        MongoCadmusRepository repository = GetRepository();
        CadmusPreviewer previewer = GetPreviewer(repository);

        string json2 = previewer.RenderFragment(ITEM_ID, ORTH_ID, index);

        string? json = repository.GetPartContent(ORTH_ID);
        Assert.NotNull(json);
        JsonDocument doc = JsonDocument.Parse(json);
        JsonElement fragments = doc.RootElement
            .GetProperty("fragments");
        JsonElement fr = GetFragmentAt(fragments, index)!.Value;
        json = fr.ToString();

        Assert.Equal(json, json2);
    }

    [Fact]
    public void BuildTextSpans_Ok()
    {
        // 0123456789-1234567
        // que bixit|annos XX
        // ..O............... 1.1@3   L0-0
        // ....O............. 1.2@1   L0-1
        // ....CCCCCCCCCCC... 1.2-2.1 L1-0
        // ................CC 2.2     L1-1

        InitDatabase();
        MongoCadmusRepository repository = GetRepository();
        CadmusPreviewer previewer = GetPreviewer(repository);

        IList<ExportedSegment> segments = previewer.BuildTextSegments(TEXT_ID,
        [
            ORTH_ID,
            COMM_ID
        ]);

        Assert.Equal(8, segments.Count);

        // qu: -
        ExportedSegment segment = segments[0];
        Assert.Equal("qu", segment.Text);
        AnnotatedTextRange? range =
            CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Empty(range.FragmentIds);
        // e: AB
        segment = segments[1];
        Assert.Equal("e", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Single(range.FragmentIds);
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.orthography@0",
            range.FragmentIds[0]);
        // _: -
        segment = segments[2];
        Assert.Equal(" ", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Empty(range.FragmentIds);
        // b: OC
        segment = segments[3];
        Assert.Equal("b", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Equal(2, range.FragmentIds.Count);
        Assert.Contains("it.vedph.token-text-layer:fr.it.vedph.orthography@1",
            range.FragmentIds);
        Assert.Contains("it.vedph.token-text-layer:fr.it.vedph.comment@0",
            range.FragmentIds);
        // ixit: C
        segment = segments[4];
        Assert.Equal("ixit", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Single(range.FragmentIds);
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.comment@0",
            range.FragmentIds[0]);
        Assert.True(segment.HasFeature(ExportedSegment.F_EOL_TAIL));

        // annos: C
        segment = segments[5];
        Assert.Equal("annos", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Single(range.FragmentIds);
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.comment@0",
            range.FragmentIds[0]);
        // _: -
        segment = segments[6];
        Assert.Equal(" ", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Empty(range.FragmentIds);
        // XX: D
        segment = segments[7];
        Assert.Equal("XX", segment.Text);
        range = CadmusTextTreeBuilder.GetSegmentFirstRange(segment);
        Assert.NotNull(range);
        Assert.Single(range.FragmentIds);
        Assert.Equal("it.vedph.token-text-layer:fr.it.vedph.comment@1",
            range.FragmentIds[0]);
    }
}
