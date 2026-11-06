using Cadmus.Export.Mapping;
using Cadmus.GraphStudio.Api.Models;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Cadmus.GraphStudio.Api.Services;

/// <summary>
/// Checks the filters of a root mapping against the source metadata used
/// for a run. In Cadmus, mappings are run only for the sources matching
/// all their filters, while the studio runs the mapping anyway: so this
/// tells the user when the mapping would not be applied in Cadmus.
/// The rules are those of <c>EfGraphRepository.IsMatch</c> (Cadmus.Graph.Ef
/// 17.1): facet, part type and part role must be equal (case-insensitively);
/// group and title must match the mapping's regular expression; flags must
/// include all the flags of the mapping; a filter on a value missing from
/// the source never matches.
/// </summary>
public static class MappingFilterChecker
{
    private static readonly TimeSpan _regexTimeout = TimeSpan.FromSeconds(1);

    private static string Show(string? value) =>
        string.IsNullOrEmpty(value) ? "(none)" : $"\"{value}\"";

    private static bool IsRegexMatch(string? input, string pattern,
        out string? error)
    {
        error = null;
        try
        {
            return Regex.IsMatch(input ?? "", pattern, RegexOptions.None,
                _regexTimeout);
        }
        catch (ArgumentException ex)
        {
            error = ex.Message;
            return false;
        }
        catch (RegexMatchTimeoutException)
        {
            error = "regular expression timeout";
            return false;
        }
    }

    private static void CheckEqual(string name, string? filter, string? value,
        string valueName, List<string> warnings)
    {
        if (!string.IsNullOrEmpty(filter) &&
            !string.Equals(filter, value, StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{name} \"{filter}\" is not equal to the " +
                $"{valueName} {Show(value)}");
        }
    }

    private static void CheckRegex(string name, string? filter, string? value,
        string valueName, List<string> warnings)
    {
        if (string.IsNullOrEmpty(filter)) return;
        if (!IsRegexMatch(value, filter, out string? error))
        {
            warnings.Add(error != null
                ? $"{name} \"{filter}\" is not a valid regular expression " +
                  $"({error}), so it never matches"
                : $"{name} \"{filter}\" does not match the " +
                  $"{valueName} {Show(value)}");
        }
    }

    /// <summary>
    /// Gets the filters of the specified mapping not matched by the specified
    /// run metadata. Only root mappings are checked, as in Cadmus the filters
    /// of children mappings are ignored.
    /// </summary>
    /// <param name="mapping">The mapping.</param>
    /// <param name="model">The run model with the source metadata.</param>
    /// <returns>Warnings, empty if all the filters match.</returns>
    /// <exception cref="ArgumentNullException">mapping or model</exception>
    public static IList<string> Check(NodeMapping mapping,
        RunMappingBindingModel model)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        ArgumentNullException.ThrowIfNull(model);

        List<string> warnings = [];
        if (mapping.ParentId != 0) return warnings;

        CheckEqual("facetFilter", mapping.FacetFilter, model.FacetId,
            "facet ID", warnings);
        CheckRegex("groupFilter", mapping.GroupFilter, model.GroupId,
            "group ID", warnings);
        CheckRegex("titleFilter", mapping.TitleFilter, model.ItemTitle,
            "item title", warnings);

        if (mapping.FlagsFilter.HasValue &&
            ((model.Flags ?? 0) & mapping.FlagsFilter.Value)
                != mapping.FlagsFilter.Value)
        {
            warnings.Add($"flagsFilter {mapping.FlagsFilter.Value} requires " +
                $"all its flags, but the item flags are {model.Flags ?? 0}");
        }

        CheckEqual("partTypeFilter", mapping.PartTypeFilter, model.PartTypeId,
            "part type ID", warnings);
        CheckEqual("partRoleFilter", mapping.PartRoleFilter, model.RoleId,
            "part role ID", warnings);

        return warnings;
    }
}
