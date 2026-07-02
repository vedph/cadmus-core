using Cadmus.Core;
using Cadmus.Export.Suppliers;
using System.Collections.Generic;
using Xunit;

namespace Cadmus.Export.Test.Suppliers;

public sealed class FlagRendererContextSupplierTest
{
    [Fact]
    public void GetFlagRendererContext_On_SuppliesPairs()
    {
        CadmusRendererContext context = new()
        {
            Source = new Item()
            {
                Flags = 0x0001 | 0x0004
            }
        };
        FlagRendererContextSupplier supplier = new();
        supplier.Configure(new FlagRendererContextSupplierOptions
        {
            On = new Dictionary<string, string>()
            {
                ["1"] = "alpha=one",
                ["4"] = "beta=four",
                ["h10"] = "gamma=sixteen",
            }
        });

        supplier.Supply(context);

        // gamma not present
        Assert.False(context.Data.ContainsKey("gamma"));

        // alpha=one
        Assert.True(context.Data.ContainsKey("alpha"));
        Assert.Equal("one", context.Data["alpha"]);

        // beta=four
        Assert.True(context.Data.ContainsKey("beta"));
        Assert.Equal("four", context.Data["beta"]);
    }

    [Fact]
    public void GetFlagRendererContext_Off_SuppliesPairs()
    {
        CadmusRendererContext context = new()
        {
            Source = new Item()
            {
                Flags = 0x0001
            }
        };
        FlagRendererContextSupplier supplier = new();
        supplier.Configure(new FlagRendererContextSupplierOptions
        {
            Off = new Dictionary<string, string>()
            {
                ["4"] = "beta=four",
                ["h10"] = "gamma=sixteen",
            }
        });

        supplier.Supply(context);

        // gamma=sixteen
        Assert.True(context.Data.ContainsKey("gamma"));
        Assert.Equal("sixteen", context.Data["gamma"]);

        // beta=four
        Assert.True(context.Data.ContainsKey("beta"));
        Assert.Equal("four", context.Data["beta"]);
    }

    [Fact]
    public void GetFlagRendererContext_Delta_RemovesEntry()
    {
        CadmusRendererContext context = new()
        {
            Source = new Item()
            {
                Flags = 0x0001 | 0x0004
            },
        };
        context.Data["alpha"] = "one";
        context.Data["beta"] = "four";

        FlagRendererContextSupplier supplier = new();
        supplier.Configure(new FlagRendererContextSupplierOptions
        {
            On = new Dictionary<string, string>()
            {
                ["1"] = "alpha=ONE",
                ["4"] = "beta",
            }
        });

        supplier.Supply(context);

        // alpha set to ONE
        Assert.True(context.Data.ContainsKey("alpha"));
        Assert.Equal("ONE", context.Data["alpha"]);

        // beta removed
        Assert.False(context.Data.ContainsKey("beta"));
    }
}
