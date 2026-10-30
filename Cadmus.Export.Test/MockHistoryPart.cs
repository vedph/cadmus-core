using CsvHelper;

namespace Cadmus.Export.Test;

[CsvRecord]
internal partial class MockHistoryPart : MockPart
{
    public string ReferenceId { get; set; } = "";
    public int Status { get; set; }
}
