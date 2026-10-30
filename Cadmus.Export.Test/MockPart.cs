using System;
using CsvHelper;

namespace Cadmus.Export.Test;

[CsvRecord]
internal partial class MockPart
{
    [CsvColumn("_id")]
    public string Id { get; set; } = "";
    public string ItemId { get; set; } = "";
    public string TypeId { get; set; } = "";
    public string? RoleId { get; set; }
    public DateTime TimeCreated { get; set; }
    public string CreatorId { get; set; } = "";
    public DateTime TimeModified { get; set; }
    public string UserId { get; set; } = "";
    public string Content { get; set; } = "{}";
}
