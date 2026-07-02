namespace Cadmus.Cli.Models;

/// <summary>
/// Options for seeded users having a first and a last name.
/// </summary>
/// <seealso cref="SeededUserOptions" />
public class NamedSeededUserOptions : SeededUserOptions
{
    /// <summary>
    /// Gets or sets the first name.
    /// </summary>
    public string? FirstName { get; set; }

    /// <summary>
    /// Gets or sets the last name.
    /// </summary>
    public string? LastName { get; set; }

    /// <summary>
    /// Returns a string representation of this user options.
    /// </summary>
    public override string ToString()
    {
        return $"{UserName} ({FirstName} {LastName}) <{Email}>";
    }
}
