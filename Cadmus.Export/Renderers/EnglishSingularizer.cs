using System;
using System.Collections.Generic;

namespace Cadmus.Export.Renderers;

/// <summary>
/// Provides English pluralization to singularization conversion.
/// This is a simple implementation focused on common patterns.
/// </summary>
internal sealed class EnglishSingularizer
{
    private readonly Dictionary<string, string> _irregularPlurals;

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="EnglishSingularizer"/> class.
    /// </summary>
    public EnglishSingularizer()
    {
        _irregularPlurals = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase)
        {
            { "children", "child" },
            { "people", "person" },
            { "men", "man" },
            { "women", "woman" },
            { "feet", "foot" },
            { "teeth", "tooth" },
            { "geese", "goose" },
            { "mice", "mouse" },
            { "oxen", "ox" },
            { "criteria", "criterion" },
            { "phenomena", "phenomenon" },
            { "data", "datum" },
            { "analyses", "analysis" },
            { "bases", "basis" },
            { "crises", "crisis" },
            { "theses", "thesis" },
            { "vertices", "vertex" },
            { "indices", "index" },
            { "matrices", "matrix" },
            { "appendices", "appendix" }
        };
    }

    /// <summary>
    /// Attempts to singularize an English plural noun.
    /// </summary>
    /// <param name="plural">The plural form.</param>
    /// <returns>The singular form, or null if no conversion
    /// could be made.</returns>
    public string? Singularize(string plural)
    {
        ArgumentNullException.ThrowIfNull(plural);

        if (string.IsNullOrWhiteSpace(plural))
            return null;

        // Check irregular plurals
        if (_irregularPlurals.TryGetValue(plural, out string? irregular))
            return irregular;

        // Handle common suffixes
        if (plural.EndsWith("ies", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 3)
        {
            // cities -> city
            return plural[..^3] + "y";
        }

        if (plural.EndsWith("ves", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 3)
        {
            // wolves -> wolf, knives -> knife
            return plural[..^3] + "f";
        }

        if (plural.EndsWith("oes", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 3)
        {
            // heroes -> hero, potatoes -> potato
            return plural[..^2];
        }

        if (plural.EndsWith("ses", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 3)
        {
            // classes -> class, buses -> bus
            return plural[..^2];
        }

        if (plural.EndsWith("xes", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 3)
        {
            // boxes -> box
            return plural[..^2];
        }

        if (plural.EndsWith("ches", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 4)
        {
            // churches -> church
            return plural[..^2];
        }

        if (plural.EndsWith("shes", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 4)
        {
            // dishes -> dish
            return plural[..^2];
        }

        if (plural.EndsWith("s", StringComparison.OrdinalIgnoreCase) &&
            plural.Length > 1 &&
            !plural.EndsWith("ss", StringComparison.OrdinalIgnoreCase))
        {
            // fragments -> fragment, items -> item
            return plural[..^1];
        }

        // no conversion possible
        return null;
    }
}
