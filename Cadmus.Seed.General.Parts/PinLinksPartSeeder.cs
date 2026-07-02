using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Cadmus.Refs.Bricks;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="PinLinksPart"/> part.
/// Tag: <c>seed.it.vedph.pin-links</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.pin-links")]
public sealed class PinLinksPartSeeder : PartSeederBase
{
    static internal List<AssertedCompositeId> GetLinks(int count)
    {
        List<AssertedCompositeId> links = new(count);
        for (int n = 1; n <= count; n++)
        {
            links.Add(new AssertedCompositeId
            {
                Target = new PinTarget
                {
                    Gid = $"http://some-resources/{n}",
                    Label = $"Mock resource #{n}"
                }
            });
        }
        return links;
    }

    /// <summary>
    /// Creates and seeds a new part.
    /// </summary>
    /// <param name="item">The item this part should belong to.</param>
    /// <param name="roleId">The optional part role ID.</param>
    /// <param name="factory">The part seeder factory. This is used
    /// for layer parts, which need to seed a set of fragments.</param>
    /// <returns>A new part or null.</returns>
    /// <exception cref="ArgumentNullException">item or factory</exception>
    public override IPart? GetPart(IItem item, string? roleId,
        PartSeederFactory? factory)
    {
        ArgumentNullException.ThrowIfNull(item);

        PinLinksPart part = new Faker<PinLinksPart>()
            .RuleFor(p => p.Links, f => GetLinks(f.Random.Number(1, 3)))
            .Generate();
        SetPartMetadata(part, roleId, item);

        return part;
    }
}
