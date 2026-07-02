using Microsoft.AspNetCore.Identity;

namespace Cadmus.Cli.Models;

/// <summary>
/// A user entity with first and last name properties, extending the
/// standard ASP.NET Core Identity user.
/// </summary>
/// <seealso cref="IdentityUser" />
public class NamedUser : IdentityUser
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
    /// Returns a string representation of this user.
    /// </summary>
    public override string ToString()
    {
        return $"{UserName} ({FirstName} {LastName})";
    }
}
