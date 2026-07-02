using Bogus;
using Cadmus.Core;
using Cadmus.General.Parts;
using Fusi.Tools.Configuration;
using System;
using System.Collections.Generic;

namespace Cadmus.Seed.General.Parts;

/// <summary>
/// Seeder for <see cref="FlagsPart"/>.
/// Tag: <c>seed.it.vedph.flags</c>.
/// </summary>
/// <seealso cref="PartSeederBase" />
[Tag("seed.it.vedph.flags")]
public sealed class FlagsPartSeeder : PartSeederBase,
    IConfigurable<FlagsPartSeederOptions>
{
    private FlagsPartSeederOptions? _options;

    /// <summary>
    /// Configures the object with the specified options.
    /// </summary>
    /// <param name="options">The options.</param>
    public void Configure(FlagsPartSeederOptions options)
    {
        _options = options;
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

        // cannot seed without options or with empty flags
        if (_options == null || _options.Flags == null ||
            _options.Flags.Count == 0)
        {
            return null;
        }

        int count = _options.Flags.Count;
        int amountToPick = count == 1 ? 1 : new Faker().Random.Number(1, count);

        FlagsPart part = new Faker<FlagsPart>()
           .RuleFor(p => p.Flags,
                f => [.. f.PickRandom(_options.Flags, amountToPick)])
           .Generate();

        // add random notes for some of the flags
        if (part.Flags.Count > 0)
        {
            Faker f = new();
            part.Notes = [];
            foreach (string flag in part.Flags)
            {
                if (f.Random.Bool(0.3f))
                    part.Notes[flag] = f.Lorem.Sentence();
            }
        }

        SetPartMetadata(part, roleId, item);

        return part;
    }
}

/// <summary>
/// Options for <see cref="FlagsPartSeeder"/>.
/// </summary>
public sealed class FlagsPartSeederOptions
{
    /// <summary>
    /// The flags to seed.
    /// </summary>
    public HashSet<string> Flags { get; set; } = [];
}
