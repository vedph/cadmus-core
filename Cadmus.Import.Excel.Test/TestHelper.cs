using Cadmus.Core;
using Cadmus.Import.Proteus;
using Proteus.Core.Entries;
using Proteus.Core.Regions;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Cadmus.Import.Excel.Test;

/// <summary>
/// Helpers for testing column and row entry region parsers.
/// </summary>
internal static class TestHelper
{
    public const string THESAURI = """
    [
      {
        "id": "colors@en",
        "entries": [
          { "id": "r", "value": "red" },
          { "id": "g", "value": "green" },
          { "id": "b", "value": "blue" },
          { "id": "lb", "value": "light blue" }
        ]
      },
      {
        "id": "my-colors@en",
        "targetId": "colors"
      },
      {
        "id": "styles@en",
        "entries": [
          { "id": "style.venetian", "value": "Venetian" },
          { "id": "style.milanese", "value": "Milanese" },
          { "id": "ico.demons", "value": "demons" },
          { "id": "ico.temptations", "value": "temptations" }
        ]
      }
    ]
    """;

    /// <summary>
    /// Creates a context with a thesaurus map and a current item.
    /// </summary>
    /// <param name="withItem">True to add a current item.</param>
    /// <returns>Context.</returns>
    public static CadmusEntrySetContext CreateContext(bool withItem = true)
    {
        ThesaurusEntryMap map = new();
        map.Load(new MemoryStream(Encoding.UTF8.GetBytes(THESAURI)));

        CadmusEntrySetContext context = new()
        {
            ThesaurusEntryMap = map
        };
        if (withItem)
        {
            context.Items.Add(new Item
            {
                FacetId = "test",
                Title = "Item title",
                CreatorId = "zeus",
                UserId = "zeus"
            });
        }
        return context;
    }

    /// <summary>
    /// Creates an exported item IDs set with a single item having EID
    /// <paramref name="eid"/>, ID <c>item-1</c>, metadata part ID <c>md-1</c>
    /// and title <c>Parent</c>, and stores it in the context data.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <param name="eid">The EID.</param>
    /// <param name="name">The optional set name.</param>
    /// <returns>The set.</returns>
    public static ExportedItemIdSet AddItemIdsSet(CadmusEntrySetContext context,
        string eid = "e1", string? name = null)
    {
        ExportedItemIdSet set = new(
        [
            new ExportedItemId
            {
                Id = "item-1",
                Title = "Parent",
                MetadataPartId = "md-1",
                Eids = [eid]
            }
        ]);
        context.Data[ImportHelper.GetItemIdsKey(name)] = set;
        return set;
    }

    /// <summary>
    /// Parses a single column region with the specified tag and value.
    /// </summary>
    /// <param name="parser">The parser.</param>
    /// <param name="context">The context.</param>
    /// <param name="tag">The column region tag.</param>
    /// <param name="value">The cell value.</param>
    /// <returns>The index of the next entry.</returns>
    public static Task<int> ParseColumnAsync(IEntryRegionParser parser,
        CadmusEntrySetContext context, string tag, string? value)
    {
        List<DecodedEntry> entries =
        [
            new DecodedCommandEntry(0, 0, "col-start", "x", "1"),
            new DecodedTextEntry(0, 0, value),
            new DecodedCommandEntry(0, 0, "col-end")
        ];
        EntrySet set = new(entries, context);
        EntryRegion region = new(0,
            new EntryRange(new EntryPoint(0), new EntryPoint(2)), tag);
        return parser.ParseAsync(set, 0, [region], 0);
    }

    /// <summary>
    /// Parses a row region with the specified parser, as entered.
    /// </summary>
    /// <param name="parser">The parser.</param>
    /// <param name="context">The context.</param>
    /// <param name="y">The row number.</param>
    /// <param name="enter">True to notify the parser that the row region
    /// was entered.</param>
    /// <returns>The index of the next entry.</returns>
    public static async Task<int> ParseRowAsync(IEntryRegionParser parser,
        CadmusEntrySetContext context, int y = 1, bool enter = true)
    {
        List<DecodedEntry> entries =
        [
            new DecodedCommandEntry(0, 0, "row-start", "y", y.ToString()),
            new DecodedCommandEntry(0, 0, "col-start", "x", "1"),
            new DecodedTextEntry(0, 0, "x"),
            new DecodedCommandEntry(0, 0, "col-end"),
            new DecodedCommandEntry(0, 0, "row-end")
        ];
        EntrySet set = new(entries, context);
        EntryRegion region = new(0,
            new EntryRange(new EntryPoint(0), new EntryPoint(4)), "row");
        if (enter) await parser.RegionChangedAsync("row", 0, true, context);
        return await parser.ParseAsync(set, 0, [region], 0);
    }
}
