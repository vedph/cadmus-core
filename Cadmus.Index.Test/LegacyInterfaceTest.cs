using Cadmus.Core;
using Fusi.Tools.Data;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Cadmus.Index.Test;

/// <summary>
/// Tests for the obsolete members of the index interfaces, which must keep
/// legacy callers working by forwarding to the new asynchronous members.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
public sealed class LegacyInterfaceTest
{
    [Fact]
    public async Task Writer_LegacyMembers_Forward()
    {
        FakeItemIndexWriter fake = new();
        IItemIndexWriter writer = fake;
        Item item = new() { Id = "i" };
        TestPart part = new() { Id = "p", ItemId = "i" };

        // legacy members complete synchronously
        Assert.True(writer.CreateIndex().IsCompleted);
        Assert.True(writer.WriteItem(item).IsCompleted);
        Assert.True(writer.WriteItems([item], CancellationToken.None)
            .IsCompleted);
        Assert.True(writer.WritePart(item, part).IsCompleted);
        Assert.True(writer.DeletePart("p").IsCompleted);
        Assert.True(writer.DeleteItem("i").IsCompleted);
        await writer.Clear();
        writer.Close();

        Assert.Equal(["create", "write:i", "writes:1", "write-part:p",
            "delete-part:p", "delete-item:i", "clear"], fake.Calls);
    }

    [Fact]
    public void Reader_LegacyMembers_Forward()
    {
        FakeItemIndexReader fake = new();
        IItemIndexReader reader = fake;

        DataPage<ItemInfo> items = reader.SearchItems("x", new PagingOptions());
        DataPage<DataPinInfo> pins = reader.SearchPins("y", new PagingOptions());
        reader.Close();

        Assert.Equal(0, items.Total);
        Assert.Equal(0, pins.Total);
        Assert.Equal(["items:x", "pins:y"], fake.Queries);
    }

    [Fact]
    public void QueryException_Ctors_Ok()
    {
        Assert.Equal(-1, new CadmusQueryException().Position);
        Assert.Equal("m", new CadmusQueryException("m").Message);
        CadmusQueryException ex = new("m", 3);
        Assert.Equal(3, ex.Position);
        System.Exception inner = new("i");
        Assert.Same(inner, new CadmusQueryException("m", inner).InnerException);
    }
}
#pragma warning restore CS0618 // Type or member is obsolete
