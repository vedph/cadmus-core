using System;
using CsvHelper.Configuration.Attributes;

namespace Cadmus.Export.Test;

internal class MockPart
{
    [Name("_id")]
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
