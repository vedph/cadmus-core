using Xunit;

namespace Cadmus.Graph.Test;

public sealed class RamUidBuilderTest
{
    [Fact]
    public void BuildUid_NoClash_AddedNoSuffix()
    {
        RamUidBuilder builder = new();

        string uid = builder.BuildUid("x:persons/john_doe", "sid1");

        Assert.Equal("x:persons/john_doe", uid);
    }

    [Fact]
    public void BuildUid_ClashUnique_FirstCallNotSuffixed()
    {
        // even when a unique UID is requested (## suffix), the very first
        // time it's generated there is no clash yet, so it must be
        // returned unsuffixed (same semantics as the DB-backed builder)
        RamUidBuilder builder = new();

        string uid = builder.BuildUid("x:timespans/ts##", "sid1");

        Assert.Equal("x:timespans/ts", uid);
    }

    [Fact]
    public void BuildUid_ClashUnique_SecondCallSuffixed()
    {
        RamUidBuilder builder = new();
        string uid1 = builder.BuildUid("x:timespans/ts##", "sid1");

        string uid2 = builder.BuildUid("x:timespans/ts##", "sid2");

        Assert.NotEqual(uid1, uid2);
        Assert.Equal("x:timespans/ts", uid1);
        Assert.StartsWith("x:timespans/ts#", uid2);
    }

    [Fact]
    public void BuildUid_ClashNotUnique_ReusedWithoutSuffix()
    {
        RamUidBuilder builder = new();
        string uid1 = builder.BuildUid("x:persons/john_doe", "sid1");

        string uid2 = builder.BuildUid("x:persons/john_doe", "sid1");

        Assert.Equal(uid1, uid2);
    }

    [Fact]
    public void BuildUid_TwoInstances_AreIndependent()
    {
        // state must not leak across instances (it's a per-store cache,
        // like a fresh in-memory DB, not process-wide global state)
        RamUidBuilder builder1 = new();
        RamUidBuilder builder2 = new();

        string uid1 = builder1.BuildUid("x:timespans/ts##", "sid1");
        string uid2 = builder2.BuildUid("x:timespans/ts##", "sid2");

        Assert.Equal("x:timespans/ts", uid1);
        Assert.Equal("x:timespans/ts", uid2);
    }
}
