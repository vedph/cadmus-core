using CsvHelper;

namespace Cadmus.Export.Test;

[CsvRecord]
internal partial class MockHistoryItem : MockItem
{
    public string ReferenceId { get; set; } = "";
    public int Status { get; set; }
}
