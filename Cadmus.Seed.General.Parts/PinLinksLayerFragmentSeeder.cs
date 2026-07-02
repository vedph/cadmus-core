using Bogus;
using Cadmus.Core;
using Cadmus.Core.Layers;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="PinLinksLayerFragment"/>'s.
/// Tag: <c>seed.fr.it.vedph.pin-links</c>.
/// </summary>
/// <seealso cref="FragmentSeederBase" />
/// <seealso cref="IConfigurable{PinLinksLayerFragmentSeederOptions}" />
[Tag("seed.fr.it.vedph.pin-links")]
public sealed class PinLinksLayerFragmentSeeder : FragmentSeederBase
{
    /// <summary>
    /// Gets the type of the fragment.
    /// </summary>
    /// <returns>Type.</returns>
    public override Type GetFragmentType() => typeof(PinLinksLayerFragment);

    /// <summary>
    /// Creates and seeds a new part.
    /// </summary>
    /// <param name="item">The item this part should belong to.</param>
    /// <param name="location">The location.</param>
    /// <param name="baseText">The base text.</param>
    /// <returns>A new fragment.</returns>
    /// <exception cref="ArgumentNullException">location or
    /// baseText</exception>
    public override ITextLayerFragment GetFragment(
        IItem item, string location, string baseText)
    {
        ArgumentNullException.ThrowIfNull(location);
        ArgumentNullException.ThrowIfNull(baseText);

        return new Faker<PinLinksLayerFragment>()
            .RuleFor(fr => fr.Location, location)
            .RuleFor(fr => fr.Links,
                f => PinLinksPartSeeder.GetLinks(f.Random.Number(1, 3)))
            .Generate();
    }
}
