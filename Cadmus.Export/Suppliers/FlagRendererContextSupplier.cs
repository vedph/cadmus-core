using Cadmus.Core;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Cadmus.Export.Suppliers;

/// <summary>
/// Flag renderer context supplier. This inspects the item's flags from
/// the received context, and for each flag bitvalue on/off and mapped in its
/// configuration it supplies a name=value pair to the context data.
/// <para>Tag: <c>it.vedph.renderer-context-supplier.flag</c>.</para>
/// </summary>
/// <seealso cref="ICadmusRendererContextSupplier" />
[Tag("it.vedph.renderer-context-supplier.flag")]
public sealed class FlagRendererContextSupplier : ICadmusRendererContextSupplier,
    IConfigurable<FlagRendererContextSupplierOptions>
{
    private readonly Dictionary<int, Tuple<string, string?>> _on = [];
    private readonly Dictionary<int, Tuple<string, string?>> _off = [];

    private static int? ParseMappingKey(string key)
    {
        int n;
        if (key.StartsWith('H') || key.StartsWith('h'))
        {
            if (!int.TryParse(key[1..], NumberStyles.HexNumber,
                CultureInfo.InvariantCulture, out n))
            {
                return null;
            }
        }
        else
        {
            if (!int.TryParse(key, out n)) return null;
        }
        return n;
    }

    private static Tuple<string, string?>? ParseMappingValue(string value)
    {
        int i = value.IndexOf('=');
        if (i == -1) return Tuple.Create(value, (string?)null);

        string n = value[..i];
        string v = value[(i + 1)..];
        return Tuple.Create(n, (string?)v);
    }

    private static void ParseMappings(IDictionary<string, string> mappings,
        Dictionary<int, Tuple<string, string?>> dct)
    {
        foreach (var kvp in mappings)
        {
            int? n = ParseMappingKey(kvp.Key);
            if (n == null) continue;

            Tuple<string, string?>? nv = ParseMappingValue(kvp.Value);
            if (nv != null) dct[n.Value] = nv;
        }
    }

    /// <summary>
    /// Configures this supplier with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <exception cref="ArgumentNullException">options</exception>
    public void Configure(FlagRendererContextSupplierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _on.Clear();
        _off.Clear();

        if (options.On != null) ParseMappings(options.On, _on);
        if (options.Off != null) ParseMappings(options.Off, _off);
    }

    /// <summary>
    /// Supplies data to the specified context.
    /// </summary>
    /// <param name="context">The context.</param>
    /// <exception cref="ArgumentNullException">context</exception>
    public void Supply(CadmusRendererContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        IItem item = (IItem)context.Source!;

        if (context.Source == null || item.Flags == 0) return;

        // on
        foreach (int n in _on.Keys)
        {
            if ((item.Flags & n) == n)
            {
                Tuple<string, string?> pair = _on[n]!;
                if (pair.Item2 == null) context.Data.Remove(pair.Item1);
                else context.Data[pair.Item1] = pair.Item2;
            }
        }

        // off
        foreach (int n in _off.Keys)
        {
            if ((item.Flags & n) == 0)
            {
                Tuple<string, string?> pair = _off[n]!;
                if (pair.Item2 == null) context.Data.Remove(pair.Item1);
                else context.Data[pair.Item1] = pair.Item2;
            }
        }
    }
}

/// <summary>
/// Options for <see cref="FlagRendererContextSupplier"/>.
/// </summary>
public class FlagRendererContextSupplierOptions
{
    /// <summary>
    /// Gets or sets the flag to pair mappings for "on" states of flags.
    /// Keys are flags (decimal values, or hexadecimal values prefixed by H or h),
    /// values are the corresponding name=value pair as a string with <c>=</c>
    /// as separator. If the value is only the name (without = and the value),
    /// the pair is removed from the context data when present.
    /// </summary>
    public IDictionary<string, string>? On { get; set; }

    /// <summary>
    /// Gets or sets the flag to pair mappings for "off" states of flags.
    /// Keys are flags (decimal values, or hexadecimal values prefixed by H or h),
    /// values are the corresponding name=value pair as a string with <c>=</c>
    /// as separator. If the value is only the name (without = and the value),
    /// the pair is removed from the context data when present.
    /// </summary>
    public IDictionary<string, string>? Off { get; set; }
}
