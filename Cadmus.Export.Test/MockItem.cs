using System;
using CsvHelper;

namespace Cadmus.Export.Test;

[CsvRecord]
internal partial class MockItem
{
    [CsvColumn("_id")]
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string FacetId { get; set; } = "";
    public string GroupId { get; set; } = "";
    public string SortKey { get; set; } = "";
    public int Flags { get; set; }
    public DateTime TimeCreated { get; set; }
    public string CreatorId { get; set; } = "";
    public DateTime TimeModified { get; set; }
    public string UserId { get; set; } = "";
}
