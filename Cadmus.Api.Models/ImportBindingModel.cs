using System.ComponentModel.DataAnnotations;

namespace Cadmus.Api.Models;

/// <summary>
/// Import entries binding model.
/// </summary>
public class ImportBindingModel
{
    /// <summary>
    /// The import mode. This is a letter: <c>R</c>=replace: if the imported
    /// entry already exists, it is fully replaced by the new one;
    /// <c>P</c>=patch: the existing thesaurus is patched with the imported one:
    /// any existing entry has its value overwritten; any non existing entry
    /// is just added; for non-thesauri imports, this is equivalent to <c>S</c>;
    /// <c>S</c>=synch: equal to patch, with the addition that
    /// any existing entry not found in the imported thesaurus is removed.
    /// </summary>
    [RegularExpression("^[rpsRPS]$")]
    public string? Mode { get; set; }

    /// <summary>
    /// The dry run flag, which, if true, causes the import to be performed
    /// without actually saving anything.
    /// </summary>
    public bool? DryRun { get; set; }
}
