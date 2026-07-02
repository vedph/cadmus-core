using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cadmus.Export;

/// <summary>
/// Token-based text exporter. This takes a <see cref="TokenTextPart"/>
/// with any number of layer parts linked to it, and produces a string
/// representing this text with a list of ranges corresponding to each
/// fragment in each of the received layers.
/// <para>Tag: <c>it.vedph.text-flattener.token</c>.</para>
/// </summary>
[Tag("it.vedph.text-flattener.token")]
public sealed class TokenTextPartFlattener : ITextPartFlattener
{
    private static int LocateTokenEnd(string text, int index)
    {
        int i = text.IndexOfAny([' ', '\n'], index);
        return i == -1 ? text.Length : i;
    }

    private static int GetIndexFromPoint(TokenTextPoint p, TokenTextPart part,
        bool end = false)
    {
        // base index for Y
        int index = (p.Y - 1) +
            part.Lines.Select(l => l.Text.Length).Take(p.Y - 1).Sum();

        // locate X
        if (p.X > 1 || end)
        {
            string line = part.Lines[p.Y - 1].Text;
            int x = p.X - 1, i = 0;
            while (x > 0)
            {
                i = LocateTokenEnd(line, i);
                if (i < line.Length) i++;
                x--;
            }
            if (end && p.At == 0)
            {
                i = LocateTokenEnd(line, i) - 1;
            }
            index += i;
        }

        // locate A
        if (p.At > 0)
        {
            index += p.At - 1;
            if (end) index += p.Run - 1;
        }

        return index;
    }

    private static AnnotatedTextRange GetRangeFromLoc(string loc, string text,
        TokenTextPart part, string frId)
    {
        TokenTextLocation l = TokenTextLocation.Parse(loc);
        int start = GetIndexFromPoint(l.A, part);

        int end;
        if (l.IsRange)
        {
            // range
            end = GetIndexFromPoint(l.B!, part, true);
        }
        else
        {
            // single point (partial/whole token)
            end = l.A.Run > 0
                ? start + l.A.Run - 1
                : LocateTokenEnd(text, start) - 1;
        }

        return new AnnotatedTextRange(start, end, frId);
    }

    private static IList<string> GetFragmentLocations(IPart part)
    {
        PropertyInfo? pi = part.GetType().GetProperty("Fragments");
        if (pi == null) return Array.Empty<string>();

        if (pi.GetValue(part) is not IEnumerable frags) return [];

        List<string> locs = [];
        foreach (object fr in frags)
        {
            locs.Add((fr as ITextLayerFragment)!.Location);
        }
        return locs;
    }

    /// <summary>
    /// Starting from a text part and a list of layer parts, gets a string
    /// representing the text with a list of layer ranges representing
    /// the extent of each layer's fragment on it.
    /// </summary>
    /// <param name="textPart">The text part used as the base text. This is
    /// the part identified by role ID <see cref="PartBase.BASE_TEXT_ROLE_ID"/>
    /// in an item.</param>
    /// <param name="layerParts">The layer parts you want to export.</param>
    /// <returns>Tuple with 1=text and 2=ranges.</returns>
    /// <exception cref="ArgumentNullException">textPart or layerParts
    /// </exception>
    public Tuple<string, IList<AnnotatedTextRange>> Flatten(IPart textPart,
        IList<IPart> layerParts)
    {
        if (textPart is not TokenTextPart ttp)
            throw new ArgumentNullException(nameof(textPart));
        ArgumentNullException.ThrowIfNull(layerParts);

        string text = string.Join("\n", ttp.Lines.Select(l => l.Text));

        // convert all the fragment locations into ranges
        IList<AnnotatedTextRange> ranges = [];
        int layerIndex = 0;
        foreach (IPart part in layerParts)
        {
            int frIndex = 0;
            foreach (string loc in GetFragmentLocations(part))
            {
                AnnotatedTextRange r = GetRangeFromLoc(loc, text, ttp,
                    $"{part.TypeId}:{part.RoleId}@{frIndex}");
                ranges.Add(r);
                frIndex++;
            }
            layerIndex++;
        }

        return Tuple.Create(text, ranges);
    }
}
