using Cadmus.General.Parts;
using Cadmus.Import.Proteus;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Import.Excel.Test;

/// <summary>
/// Tests for options bound from JSON configuration, as the pipeline factory
/// does. Configuration binding appends configured items to the items already
/// present in collection properties, so options must not have non-empty
/// default collections.
/// </summary>
public sealed class OptionsBindingTest
{
    private static T Bind<T>(string json) where T : new()
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddJsonStream(new MemoryStream(Encoding.UTF8.GetBytes(json)))
            .Build();
        return config.GetSection("options").Get<T>() ?? new T();
    }

    [Fact]
    public async Task ColMeasures_BoundNames_ReplaceDefaults()
    {
        ColMeasuresEntryRegionParserOptions options =
            Bind<ColMeasuresEntryRegionParserOptions>("""
            {
              "options": {
                "tag": "col-size",
                "names": ["height", "width"],
                "separators": ["*"]
              }
            }
            """);
        Assert.Equal(["height", "width"], options.Names!);
        Assert.Equal(['*'], options.Separators!);

        ColMeasuresEntryRegionParser parser = new();
        parser.Configure(options);
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(parser, context, "col-size", "48*75");

        Assert.Equal(2, context.CurrentItem!.Parts
            .OfType<PhysicalMeasurementsPart>().Single().Measurements.Count);
    }

    [Fact]
    public async Task ColMeasures_Unbound_UsesDefaults()
    {
        ColMeasuresEntryRegionParser parser = new();
        parser.Configure(Bind<ColMeasuresEntryRegionParserOptions>(
            """{ "options": { "tag": "col-size" } }"""));
        CadmusEntrySetContext context = TestHelper.CreateContext();

        await TestHelper.ParseColumnAsync(parser, context, "col-size", "48x75");

        Assert.Equal(["height", "width"], context.CurrentItem!.Parts
            .OfType<PhysicalMeasurementsPart>().Single().Measurements
            .Select(m => m.Name));
    }

    [Fact]
    public void AllOptions_NoNonEmptyDefaultCollections()
    {
        IEnumerable<Type> types = typeof(ImportHelper).Assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract &&
                t.Name.EndsWith("Options", StringComparison.Ordinal) &&
                t.GetConstructor(Type.EmptyTypes) != null);

        foreach (Type type in types)
        {
            object options = Activator.CreateInstance(type)!;
            foreach (PropertyInfo p in type.GetProperties())
            {
                if (p.PropertyType == typeof(string) ||
                    !typeof(System.Collections.IEnumerable)
                        .IsAssignableFrom(p.PropertyType))
                {
                    continue;
                }
                if (p.GetValue(options) is System.Collections.IEnumerable e)
                {
                    Assert.False(e.GetEnumerator().MoveNext(),
                        $"{type.Name}.{p.Name} has a non-empty default");
                }
            }
        }
    }
}
