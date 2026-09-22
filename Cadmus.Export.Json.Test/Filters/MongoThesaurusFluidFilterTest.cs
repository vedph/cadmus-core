using Cadmus.Export.Json.Filters;
using Cadmus.Mongo;
using Fluid;
using Fluid.Values;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;
using System;
using System.Threading.Tasks;

namespace Cadmus.Export.Json.Test.Filters;

public sealed class MongoThesaurusFluidFilterTest
{
    private const string DB_NAME = "cadmus-fluid-test";
    private const string CS = "mongodb://localhost:27017/" + DB_NAME;

    private static void InitDatabase()
    {
        // ensure documents are written with the same camelCase element
        // naming convention that MongoConsumerBase (and thus the filter
        // under test) registers, regardless of whether a filter instance
        // has already been created in this process
        ConventionPack pack = new() { new CamelCaseElementNameConvention() };
        ConventionRegistry.Register("camel case", pack, _ => true);

        MongoClient client = new(CS);
        client.DropDatabase(DB_NAME);
        IMongoDatabase db = client.GetDatabase(DB_NAME);

        MongoThesaurus thesaurus = new()
        {
            Id = "languages@en",
            Entries =
            [
                new MongoThesaurusEntry { Id = "eng", Value = "English" },
                new MongoThesaurusEntry { Id = "fre", Value = "French" },
                new MongoThesaurusEntry { Id = "deu", Value = "German" },
                new MongoThesaurusEntry { Id = "grc", Value = "Ancient Greek" },
                new MongoThesaurusEntry { Id = "gre", Value = "Modern Greek" },
                new MongoThesaurusEntry { Id = "ita", Value = "Italian" },
                new MongoThesaurusEntry { Id = "lat", Value = "Latin" },
                new MongoThesaurusEntry { Id = "spa", Value = "Spanish" },
            ]
        };
        db.GetCollection<MongoThesaurus>(MongoThesaurus.COLLECTION)
            .InsertOne(thesaurus);
    }

    private static MongoThesaurusFluidFilter GetConfiguredFilter()
    {
        MongoThesaurusFluidFilter filter = new();
        filter.Configure(new MongoThesRendererFilterOptions
        {
            ConnectionString = CS
        });
        return filter;
    }

    private static Task<FluidValue> ApplyAsync(IFluidFilter filter, string input)
    {
        return filter.Apply(new StringValue(input), FilterArguments.Empty,
            new TemplateContext()).AsTask();
    }

    [Fact]
    public async Task Apply_MatchingReference_ReturnsEntryValue()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue result = await ApplyAsync(filter, "$[languages|grc]");

        Assert.Equal("Ancient Greek", result.ToStringValue());
    }

    [Fact]
    public async Task Apply_EmbeddedInLargerText_PreservesSurroundingText()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue result = await ApplyAsync(filter,
            "Language: $[languages|eng].");

        Assert.Equal("Language: English.", result.ToStringValue());
    }

    [Fact]
    public async Task Apply_MultipleReferences_AllReplaced()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue result = await ApplyAsync(filter,
            "$[languages|grc] and $[languages|ita]");

        Assert.Equal("Ancient Greek and Italian", result.ToStringValue());
    }

    [Fact]
    public async Task Apply_UnknownEntry_ReturnsInputUnchanged()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue result = await ApplyAsync(filter, "$[languages|xyz]");

        Assert.Equal("$[languages|xyz]", result.ToStringValue());
    }

    [Fact]
    public async Task Apply_UnknownThesaurus_ReturnsInputUnchanged()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue result = await ApplyAsync(filter, "$[nope|grc]");

        Assert.Equal("$[nope|grc]", result.ToStringValue());
    }

    [Fact]
    public async Task Apply_NoReferenceInInput_ReturnsInputUnchanged()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue result = await ApplyAsync(filter, "just plain text");

        Assert.Equal("just plain text", result.ToStringValue());
    }

    [Fact]
    public async Task Apply_NonStringInput_ReturnsInputUnchanged()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue input = NumberValue.Create(42);
        FluidValue result = await filter.Apply(input, FilterArguments.Empty,
            new TemplateContext());

        Assert.Equal(42, (int)result.ToNumberValue());
    }

    [Fact]
    public async Task Apply_RepeatedCallsSameThesaurus_ResolveFromCache()
    {
        InitDatabase();
        MongoThesaurusFluidFilter filter = GetConfiguredFilter();

        FluidValue first = await ApplyAsync(filter, "$[languages|grc]");
        Assert.Equal("Ancient Greek", first.ToStringValue());

        // even after the underlying thesaurus is gone, a previously cached
        // lookup for the same filter instance should still resolve
        MongoClient client = new(CS);
        client.GetDatabase(DB_NAME).DropCollection(MongoThesaurus.COLLECTION,
            TestContext.Current.CancellationToken);

        FluidValue second = await ApplyAsync(filter, "$[languages|ita]");
        Assert.Equal("Italian", second.ToStringValue());
    }

    [Fact]
    public async Task Apply_NotConfigured_Throws()
    {
        MongoThesaurusFluidFilter filter = new();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ApplyAsync(filter, "$[languages|grc]"));
    }
}
