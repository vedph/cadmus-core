namespace Cadmus.Cli.Models;

/// <summary>
/// Options for a seeded user account loaded from JSON configuration.
/// </summary>
public class SeededUserOptions
{
    /// <summary>
    /// Gets or sets the username.
    /// </summary>
    public string? UserName { get; set; }

    /// <summary>
    /// Gets or sets the password.
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Gets or sets the email address.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Gets or sets the roles assigned to this user.
    /// </summary>
    public string[]? Roles { get; set; }

    /// <summary>
    /// Returns a string representation of this user options.
    /// </summary>
    public override string ToString()
    {
        return $"{UserName} <{Email}>";
    }
}
