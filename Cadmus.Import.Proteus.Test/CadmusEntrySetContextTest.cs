using Cadmus.Core;
using Cadmus.General.Parts;
using Xunit;

namespace Cadmus.Import.Proteus.Test;

public sealed class CadmusEntrySetContextTest
{
    [Fact]
    public void EnsurePartForCurrentItem_NotExisting_Added()
    {
        CadmusEntrySetContext context = new();
        IItem item = new Item
        {
            Id = "1",
            Title = "Test"
        };
        context.Items.Add(item);

        NotePart part = context.EnsurePartForCurrentItem<NotePart>();

        Assert.NotNull(part);
        Assert.Equal(item.Id, part.ItemId);
    }

    [Fact]
    public void EnsurePartForCurrentItem_Existing_Fetched()
    {
        CadmusEntrySetContext context = new();
        IItem item = new Item
        {
            Id = "1",
            Title = "Test"
        };
        context.Items.Add(item);

        NotePart part = context.EnsurePartForCurrentItem<NotePart>();
        NotePart part2 = context.EnsurePartForCurrentItem<NotePart>();

        Assert.NotNull(part);
        Assert.Same(part2, part);
    }

    [Fact]
    public void EnsurePartForCurrentItem_ExistingDifferentRole_Added()
    {
        CadmusEntrySetContext context = new();
        IItem item = new Item
        {
            Id = "1",
            Title = "Test"
        };
        context.Items.Add(item);

        NotePart part = context.EnsurePartForCurrentItem<NotePart>();
        NotePart part2 = context.EnsurePartForCurrentItem<NotePart>("x");

        Assert.NotNull(part2);
        Assert.Equal(item.Id, part2.ItemId);

        Assert.NotSame(part2, part);
        Assert.Equal(2, item.Parts.Count);
    }

    [Fact]
    public void EnsurePartForCurrentItem_ExistingSameRole_Fetched()
    {
        CadmusEntrySetContext context = new();
        IItem item = new Item
        {
            Id = "1",
            Title = "Test"
        };
        context.Items.Add(item);

        NotePart part = context.EnsurePartForCurrentItem<NotePart>("x");
        NotePart part2 = context.EnsurePartForCurrentItem<NotePart>("x");

        Assert.NotNull(part);
        Assert.Same(part2, part);
    }
}
