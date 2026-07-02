using Cadmus.Export.Renderers;
using Proteus.Core.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace Cadmus.Export.Test.Renderers;

/// <summary>
/// Unit tests for <see cref="JsonToXmlConverter"/>.
/// </summary>
public sealed class JsonToXmlConverterTest
{
    #region Basic Conversion Tests
    [Fact]
    public void Convert_NullJson_ThrowsArgumentNullException()
    {
        JsonToXmlConverter converter = new();

        Assert.Throws<ArgumentNullException>(
            () => converter.Convert(null!));
    }

    [Fact]
    public void Convert_EmptyObject_ReturnsRootElement()
    {
        JsonToXmlConverter converter = new();
        string json = "{}";

        XElement result = converter.Convert(json);

        Assert.NotNull(result);
        Assert.Equal("root", result.Name.LocalName);
        Assert.Empty(result.Elements());
    }

    [Fact]
    public void Convert_SimpleProperties_CreatesElements()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""name"": ""John"",
            ""age"": 30,
            ""active"": true
        }";

        XElement result = converter.Convert(json);

        Assert.Equal("root", result.Name.LocalName);
        Assert.Equal("John", result.Element("name")?.Value);
        Assert.Equal("30", result.Element("age")?.Value);
        Assert.Equal("true", result.Element("active")?.Value);
    }

    [Fact]
    public void Convert_NestedObject_CreatesNestedElements()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""person"": {
                ""name"": ""Jane"",
                ""age"": 25
            }
        }";

        XElement result = converter.Convert(json);

        XElement? person = result.Element("person");
        Assert.NotNull(person);
        Assert.Equal("Jane", person.Element("name")?.Value);
        Assert.Equal("25", person.Element("age")?.Value);
    }

    [Fact]
    public void Convert_Array_CreatesContainerWithItems()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""items"": [""apple"", ""banana"", ""cherry""]
        }";

        XElement result = converter.Convert(json);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);

        // Items should be singularized to "item"
        List<XElement> items = [.. itemsContainer.Elements("item")];
        Assert.Equal(3, items.Count);
        Assert.Equal("apple", items[0].Value);
        Assert.Equal("banana", items[1].Value);
        Assert.Equal("cherry", items[2].Value);
    }

    [Fact]
    public void Convert_ArrayOfObjects_CreatesContainerWithItemChildren()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""users"": [
                { ""name"": ""Alice"", ""age"": 20 },
                { ""name"": ""Bob"", ""age"": 30 }
            ]
        }";

        XElement result = converter.Convert(json);

        XElement? usersContainer = result.Element("users");
        Assert.NotNull(usersContainer);

        // Users should be singularized to "user"
        List<XElement> users = [.. usersContainer.Elements("user")];
        Assert.Equal(2, users.Count);
        Assert.Equal("Alice", users[0].Element("name")?.Value);
        Assert.Equal("20", users[0].Element("age")?.Value);
        Assert.Equal("Bob", users[1].Element("name")?.Value);
        Assert.Equal("30", users[1].Element("age")?.Value);
    }
    #endregion

    #region Option: RootElementName
    [Fact]
    public void Convert_CustomRootElementName_UsesCustomName()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            RootElementName = "data"
        };
        string json = @"{ ""name"": ""test"" }";

        XElement result = converter.Convert(json, options);

        Assert.Equal("data", result.Name.LocalName);
        Assert.Equal("test", result.Element("name")?.Value);
    }

    [Fact]
    public void Convert_PrefixedRootElementName_UsesNamespace()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            RootElementName = "tei:TEI"
        };
        string json = @"{ ""title"": ""Test"" }";

        XElement result = converter.Convert(json, options);

        Assert.Equal("TEI", result.Name.LocalName);
        Assert.Equal(NamespaceOptions.TEI, result.Name.Namespace);
    }
    #endregion

    #region Option: NoNullValues
    [Fact]
    public void Convert_NoNullValues_False_IncludesNulls()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            NoNullValues = false
        };
        string json = @"{
            ""name"": ""John"",
            ""middle"": null,
            ""age"": 30
        }";

        XElement result = converter.Convert(json, options);

        Assert.NotNull(result.Element("name"));
        Assert.NotNull(result.Element("middle"));
        Assert.Empty(result.Element("middle")!.Value);
        Assert.NotNull(result.Element("age"));
    }

    [Fact]
    public void Convert_NoNullValues_True_ExcludesNulls()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            NoNullValues = true
        };
        string json = @"{
            ""name"": ""John"",
            ""middle"": null,
            ""age"": 30
        }";

        XElement result = converter.Convert(json, options);

        Assert.NotNull(result.Element("name"));
        Assert.Null(result.Element("middle"));
        Assert.NotNull(result.Element("age"));
    }
    #endregion

    #region Option: NoFalseValues
    [Fact]
    public void Convert_NoFalseValues_False_IncludesFalse()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            NoFalseValues = false
        };
        string json = @"{
            ""enabled"": true,
            ""disabled"": false,
            ""count"": 0
        }";

        XElement result = converter.Convert(json, options);

        Assert.NotNull(result.Element("enabled"));
        Assert.NotNull(result.Element("disabled"));
        Assert.Equal("false", result.Element("disabled")!.Value);
        Assert.NotNull(result.Element("count"));
    }

    [Fact]
    public void Convert_NoFalseValues_True_ExcludesFalse()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            NoFalseValues = true
        };
        string json = @"{
            ""enabled"": true,
            ""disabled"": false,
            ""count"": 0
        }";

        XElement result = converter.Convert(json, options);

        Assert.NotNull(result.Element("enabled"));
        Assert.Null(result.Element("disabled"));
        Assert.NotNull(result.Element("count"));
    }
    #endregion

    #region Option: NoZeroValues
    [Fact]
    public void Convert_NoZeroValues_False_IncludesZeros()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            NoZeroValues = false
        };
        string json = @"{
            ""count"": 0,
            ""price"": 0.0,
            ""quantity"": 10
        }";

        XElement result = converter.Convert(json, options);

        Assert.NotNull(result.Element("count"));
        Assert.Equal("0", result.Element("count")!.Value);
        Assert.NotNull(result.Element("price"));
        Assert.NotNull(result.Element("quantity"));
    }

    [Fact]
    public void Convert_NoZeroValues_True_ExcludesZeros()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            NoZeroValues = true
        };
        string json = @"{
            ""count"": 0,
            ""price"": 0.0,
            ""quantity"": 10
        }";

        XElement result = converter.Convert(json, options);

        Assert.Null(result.Element("count"));
        Assert.Null(result.Element("price"));
        Assert.NotNull(result.Element("quantity"));
        Assert.Equal("10", result.Element("quantity")!.Value);
    }
    #endregion

    #region Option: ArrayItemNumbering
    [Fact]
    public void Convert_ArrayItemNumbering_False_NoNumbering()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            ArrayItemNumbering = false
        };
        string json = @"{
            ""items"": [""a"", ""b"", ""c""]
        }";

        XElement result = converter.Convert(json, options);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);

        List<XElement> items = [.. itemsContainer.Elements("item")];
        Assert.All(items, item => Assert.Null(item.Attribute("n")));
    }

    [Fact]
    public void Convert_ArrayItemNumbering_True_AddsNumbering()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            ArrayItemNumbering = true
        };
        string json = @"{
            ""items"": [""a"", ""b"", ""c""]
        }";

        XElement result = converter.Convert(json, options);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);

        List<XElement> items = [.. itemsContainer.Elements("item")];
        Assert.Equal(3, items.Count);
        Assert.Equal("1", items[0].Attribute("n")?.Value);
        Assert.Equal("2", items[1].Attribute("n")?.Value);
        Assert.Equal("3", items[2].Attribute("n")?.Value);
    }
    #endregion

    #region Array Structure Tests

    [Fact]
    public void Convert_SingleItemArray_CreatesContainerWithOneItem()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""items"": [""single""]
        }";

        XElement result = converter.Convert(json);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);

        List<XElement> items = [.. itemsContainer.Elements("item")];
        Assert.Single(items);
        Assert.Equal("single", items[0].Value);
    }

    [Fact]
    public void Convert_EmptyArray_CreatesEmptyContainer()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""items"": []
        }";

        XElement result = converter.Convert(json);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);
        Assert.Empty(itemsContainer.Elements());
    }

    #endregion

    #region Option: WrappedEntryNames
    [Fact]
    public void Convert_WrappedEntryNames_UsesExplicitMapping()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            WrappedEntryNames = new Dictionary<string, string>
            {
                { "items", "element" }  // Use "element" instead of "item"
            }
        };
        string json = @"{
            ""items"": [{ ""name"": ""test"" }]
        }";

        XElement result = converter.Convert(json, options);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);
        XElement? item = itemsContainer.Element("element");
        Assert.NotNull(item);
        Assert.Equal("test", item.Element("name")?.Value);
    }

    [Fact]
    public void Convert_WrappedEntryNames_MultipleElements_WrapsAll()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            WrappedEntryNames = new Dictionary<string, string>
            {
                { "items", "item" }
            }
        };
        string json = @"{
            ""items"": [
                { ""name"": ""first"" },
                { ""name"": ""second"" },
                { ""name"": ""third"" }
            ]
        }";

        XElement result = converter.Convert(json, options);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);

        List<XElement> items = [.. itemsContainer.Elements("item")];
        Assert.Equal(3, items.Count);
        Assert.Equal("first", items[0].Element("name")?.Value);
        Assert.Equal("second", items[1].Element("name")?.Value);
        Assert.Equal("third", items[2].Element("name")?.Value);
    }

    [Fact]
    public void Convert_WrappedEntryNames_WithNumbering_CombinesBoth()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            WrappedEntryNames = new Dictionary<string, string>
            {
                { "items", "item" }
            },
            ArrayItemNumbering = true
        };
        string json = @"{
            ""items"": [
                { ""name"": ""first"" },
                { ""name"": ""second"" }
            ]
        }";

        XElement result = converter.Convert(json, options);

        XElement? itemsContainer = result.Element("items");
        Assert.NotNull(itemsContainer);

        List<XElement> items = [.. itemsContainer.Elements("item")];
        Assert.Equal(2, items.Count);
        Assert.Equal("1", items[0].Attribute("n")?.Value);
        Assert.Equal("2", items[1].Attribute("n")?.Value);
    }
    #endregion

    #region Singularization Tests
    [Fact]
    public void Convert_AutoSingularization_Fragments_UsesFragment()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""fragments"": [
                { ""text"": ""one"" },
                { ""text"": ""two"" }
            ]
        }";

        XElement result = converter.Convert(json);

        XElement? fragmentsContainer = result.Element("fragments");
        Assert.NotNull(fragmentsContainer);

        // Auto-singularization: "fragments" -> "fragment"
        List<XElement> fragments =
            [.. fragmentsContainer.Elements("fragment")];
        Assert.Equal(2, fragments.Count);
        Assert.Equal("one", fragments[0].Element("text")?.Value);
        Assert.Equal("two", fragments[1].Element("text")?.Value);
    }

    [Fact]
    public void Convert_ComplexArray_HandlesNestedStructures()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            WrappedEntryNames = new Dictionary<string, string>
            {
                { "users", "user" },
                { "addresses", "address" }
            }
        };
        string json = @"{
            ""users"": [
                {
                    ""name"": ""John"",
                    ""addresses"": [
                        { ""city"": ""NYC"" },
                        { ""city"": ""LA"" }
                    ]
                }
            ]
        }";

        XElement result = converter.Convert(json, options);

        XElement? usersContainer = result.Element("users");
        Assert.NotNull(usersContainer);

        XElement? user = usersContainer.Element("user");
        Assert.NotNull(user);
        Assert.Equal("John", user.Element("name")?.Value);

        XElement? addressesContainer = user.Element("addresses");
        Assert.NotNull(addressesContainer);

        List<XElement> addresses =
            [.. addressesContainer.Elements("address")];
        Assert.Equal(2, addresses.Count);
    }
    #endregion

    #region Edge Cases

    [Fact]
    public void Convert_MixedTypes_HandlesAll()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""string"": ""text"",
            ""number"": 42,
            ""decimal"": 3.14,
            ""true"": true,
            ""false"": false,
            ""null"": null,
            ""array"": [1, 2, 3],
            ""object"": { ""nested"": ""value"" }
        }";

        XElement result = converter.Convert(json);

        Assert.Equal("text", result.Element("string")?.Value);
        Assert.Equal("42", result.Element("number")?.Value);
        Assert.Equal("3.14", result.Element("decimal")?.Value);
        Assert.Equal("true", result.Element("true")?.Value);
        Assert.Equal("false", result.Element("false")?.Value);
        Assert.NotNull(result.Element("null"));

        // Array becomes container with singularized items
        XElement? arrayContainer = result.Element("array");
        Assert.NotNull(arrayContainer);
        Assert.Equal(3, arrayContainer.Elements().Count());

        Assert.NotNull(result.Element("object"));
    }

    [Fact]
    public void Convert_SpecialCharactersInStrings_PreservesContent()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""text"": ""<html>&nbsp;</html>""
        }";

        XElement result = converter.Convert(json);

        Assert.Equal("<html>&nbsp;</html>",
            result.Element("text")?.Value);
    }

    [Fact]
    public void Convert_UnicodeCharacters_PreservesContent()
    {
        JsonToXmlConverter converter = new();
        string json = @"{
            ""greek"": ""Ἑλληνικά"",
            ""emoji"": ""🔥💯""
        }";

        XElement result = converter.Convert(json);

        Assert.Equal("Ἑλληνικά", result.Element("greek")?.Value);
        Assert.Equal("🔥💯", result.Element("emoji")?.Value);
    }
    #endregion

    #region Combined Options Tests
    [Fact]
    public void Convert_CombinedOptions_AppliesAll()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            RootElementName = "document",
            NoNullValues = true,
            NoFalseValues = true,
            NoZeroValues = true,
            ArrayItemNumbering = true,
            WrappedEntryNames = new Dictionary<string, string>
            {
                { "entries", "entry" }
            }
        };
        string json = @"{
            ""name"": ""Test"",
            ""count"": 0,
            ""active"": false,
            ""missing"": null,
            ""entries"": [
                { ""id"": 1 },
                { ""id"": 2 }
            ]
        }";

        XElement result = converter.Convert(json, options);

        // Root name
        Assert.Equal("document", result.Name.LocalName);

        // Included properties
        Assert.NotNull(result.Element("name"));

        // Excluded properties
        Assert.Null(result.Element("count"));
        Assert.Null(result.Element("active"));
        Assert.Null(result.Element("missing"));

        // Array wrapping and numbering
        XElement? entriesContainer = result.Element("entries");
        Assert.NotNull(entriesContainer);

        List<XElement> entries =
            [.. entriesContainer.Elements("entry")];
        Assert.Equal(2, entries.Count);
        Assert.Equal("1", entries[0].Attribute("n")?.Value);
        Assert.Equal("2", entries[1].Attribute("n")?.Value);
    }
    #endregion

    #region Namespace Tests
    [Fact]
    public void Convert_WithNamespace_AppliesNamespace()
    {
        JsonToXmlConverter converter = new();
        JsonToXmlConverterOptions options = new()
        {
            RootElementName = "tei:TEI",
            WrappedEntryNames = new Dictionary<string, string>
            {
                { "divs", "tei:div" }
            }
        };
        string json = @"{
            ""divs"": [
                { ""type"": ""chapter"" }
            ]
        }";

        XElement result = converter.Convert(json, options);

        Assert.Equal("TEI", result.Name.LocalName);
        Assert.Equal(NamespaceOptions.TEI, result.Name.Namespace);

        XElement? divsContainer = result.Element("divs");
        Assert.NotNull(divsContainer);

        XElement? div = divsContainer.Elements().FirstOrDefault(
            e => e.Name.LocalName == "div" &&
                 e.Name.Namespace == NamespaceOptions.TEI);
        Assert.NotNull(div);
    }
    #endregion
}
