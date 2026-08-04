using System.Text.Json;
using System.Collections.Generic;
using Cadmus.Codicology.Parts;

namespace Cadmus.Codicology.Mapping.Test;

public class CodLocationMacroTest
{
    private readonly JsonSerializerOptions _options;

    public CodLocationMacroTest()
    {
        _options = new JsonSerializerOptions
        {
            AllowTrailingCommas = true,
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Fact]
    public void Run_Null_Empty()
    {
        CodLocationMacro m = new();
        string? s = m.Run(null, null);
        Assert.Null(s);
    }

    [Fact]
    public void Run_Location()
    {
        CodLocation loc = new()
        {
            S = "x",
            N = 12,
            Rmn = true
        };
        string json = JsonSerializer.Serialize(loc, _options);
        CodLocationMacro m = new();

        string? s = m.Run(null, [json]);

        Assert.Equal("x:^12", s);
    }

    [Fact]
    public void Run_LocationRange()
    {
        CodLocationRange range = new()
        {
            Start = new CodLocation
            {
                S = "x",
                N = 12,
                Rmn = true
            },
            End = new CodLocation
            {
                S = "y",
                N = 13,
                Rmn = false
            }
        };
        string json = JsonSerializer.Serialize(range, _options);
        CodLocationMacro m = new();

        string? s = m.Run(null, [json]);

        Assert.Equal("x:^12-y:13", s);
    }

    [Fact]
    public void Run_LocationRangeArray()
    {
        List<CodLocationRange> ranges =
        [
            new CodLocationRange
            {
                Start = new CodLocation
                {
                    S = "x",
                    N = 12,
                },
                End = new CodLocation
                {
                    S = "x",
                    N = 13,
                }
            },
            new CodLocationRange
            {
                Start = new CodLocation
                {
                    S = "y",
                    N = 14,
                },
                End = new CodLocation
                {
                    S = "y",
                    N = 15,
                }
            }
        ];
        string json = JsonSerializer.Serialize(ranges, _options);
        CodLocationMacro m = new();

        string? s = m.Run(null, [json]);

        Assert.Equal("x:12-x:13 y:14-y:15", s);
    }
}