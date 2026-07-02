using Cadmus.Cli.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Spectre.Console;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Cadmus.Cli.Services;

/// <summary>
/// Result of a user seeding operation.
/// </summary>
public sealed class UserSeedResult
{
    /// <summary>
    /// Gets or sets the number of users processed.
    /// </summary>
    public int UsersProcessed { get; set; }

    /// <summary>
    /// Gets or sets the number of users created.
    /// </summary>
    public int UsersCreated { get; set; }

    /// <summary>
    /// Gets or sets the number of users updated.
    /// </summary>
    public int UsersUpdated { get; set; }

    /// <summary>
    /// Gets or sets the number of roles created.
    /// </summary>
    public int RolesCreated { get; set; }

    /// <summary>
    /// Gets or sets the number of role assignments added.
    /// </summary>
    public int RoleAssignmentsAdded { get; set; }

    /// <summary>
    /// Gets or sets the errors that occurred during seeding.
    /// </summary>
    public List<string> Errors { get; } = [];
}

/// <summary>
/// Service for seeding users into the authentication database using
/// Entity Framework Core directly, without requiring the full ASP.NET
/// Core Identity services stack.
/// </summary>
public sealed class UserSeeder
{
    private readonly AuthDbContext _context;
    private readonly PasswordHasher<NamedUser> _passwordHasher;
    private readonly Action<string>? _logAction;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserSeeder"/> class.
    /// </summary>
    /// <param name="context">The authentication database context.</param>
    /// <param name="logAction">Optional action to log messages.</param>
    /// <exception cref="ArgumentNullException">context is null.</exception>
    public UserSeeder(AuthDbContext context, Action<string>? logAction = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        _context = context;
        _passwordHasher = new PasswordHasher<NamedUser>();
        _logAction = logAction;
    }

    private void Log(string message)
    {
        _logAction?.Invoke(message);
    }

    /// <summary>
    /// Loads seeded user options from a JSON file.
    /// </summary>
    /// <param name="jsonFilePath">The path to the JSON file.</param>
    /// <returns>Array of user options.</returns>
    /// <exception cref="ArgumentNullException">jsonFilePath is null.</exception>
    /// <exception cref="FileNotFoundException">File not found.</exception>
    /// <exception cref="JsonException">Invalid JSON format.</exception>
    public static NamedSeededUserOptions[] LoadUsersFromJson(string jsonFilePath)
    {
        ArgumentNullException.ThrowIfNull(jsonFilePath);

        if (!File.Exists(jsonFilePath))
        {
            throw new FileNotFoundException(
                $"User seed file not found: {jsonFilePath}",
                jsonFilePath);
        }

        string json = File.ReadAllText(jsonFilePath);

        JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        return JsonSerializer.Deserialize<NamedSeededUserOptions[]>(json, options)
            ?? [];
    }

    /// <summary>
    /// Validates the user options loaded from JSON.
    /// </summary>
    /// <param name="users">The users to validate.</param>
    /// <returns>List of validation errors, empty if all valid.</returns>
    public static List<string> ValidateUsers(
        IReadOnlyList<NamedSeededUserOptions> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        List<string> errors = [];

        for (int i = 0; i < users.Count; i++)
        {
            NamedSeededUserOptions user = users[i];

            if (string.IsNullOrWhiteSpace(user.UserName))
                errors.Add($"User #{i + 1}: UserName is required");

            if (string.IsNullOrWhiteSpace(user.Password))
                errors.Add($"User #{i + 1} ({user.UserName}): Password is required");

            if (string.IsNullOrWhiteSpace(user.Email))
                errors.Add($"User #{i + 1} ({user.UserName}): Email is required");
        }

        return errors;
    }

    /// <summary>
    /// Ensures that all roles from the user options exist in the database.
    /// </summary>
    /// <param name="users">The users whose roles to ensure.</param>
    /// <returns>Number of roles created.</returns>
    private async Task<int> EnsureRolesAsync(
        IReadOnlyList<NamedSeededUserOptions> users)
    {
        // Collect all unique role names
        HashSet<string> roleNames = users
            .Where(u => u.Roles != null)
            .SelectMany(u => u.Roles!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (roleNames.Count == 0)
            return 0;

        // Get existing roles
        HashSet<string> existingRoles = (await _context.Roles
            .Select(r => r.NormalizedName!)
            .ToListAsync())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        int created = 0;

        foreach (string roleName in roleNames)
        {
            string normalizedName = roleName.ToUpperInvariant();
            if (!existingRoles.Contains(normalizedName))
            {
                Log($"  Creating role: {roleName}");

                IdentityRole role = new()
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = roleName,
                    NormalizedName = normalizedName,
                    ConcurrencyStamp = Guid.NewGuid().ToString()
                };

                _context.Roles.Add(role);
                existingRoles.Add(normalizedName);
                created++;
            }
        }

        if (created > 0)
            await _context.SaveChangesAsync();

        return created;
    }

    /// <summary>
    /// Seeds a single user into the database.
    /// </summary>
    /// <param name="options">The user options.</param>
    /// <param name="result">The result to update.</param>
    private async Task SeedUserAsync(
        NamedSeededUserOptions options,
        UserSeedResult result)
    {
        string normalizedUserName = options.UserName!.ToUpperInvariant();
        string normalizedEmail = options.Email!.ToUpperInvariant();

        // Check if user already exists
        NamedUser? existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.NormalizedUserName == normalizedUserName);

        NamedUser user;
        bool isNewUser = existingUser == null;

        if (isNewUser)
        {
            Log($"  Creating user: {options.UserName}");

            user = new NamedUser
            {
                Id = Guid.NewGuid().ToString(),
                UserName = options.UserName,
                NormalizedUserName = normalizedUserName,
                Email = options.Email,
                NormalizedEmail = normalizedEmail,
                EmailConfirmed = true,
                SecurityStamp = Guid.NewGuid().ToString(),
                ConcurrencyStamp = Guid.NewGuid().ToString(),
                FirstName = options.FirstName,
                LastName = options.LastName
            };

            // Hash the password
            user.PasswordHash = _passwordHasher.HashPassword(user, options.Password!);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            result.UsersCreated++;
        }
        else
        {
            user = existingUser!;
            bool updated = false;

            // Update email confirmation if not confirmed
            if (!user.EmailConfirmed)
            {
                Log($"  Confirming email for: {options.UserName}");
                user.EmailConfirmed = true;
                updated = true;
            }

            // Update first/last name if changed
            if (user.FirstName != options.FirstName ||
                user.LastName != options.LastName)
            {
                Log($"  Updating name for: {options.UserName}");
                user.FirstName = options.FirstName;
                user.LastName = options.LastName;
                updated = true;
            }

            if (updated)
            {
                await _context.SaveChangesAsync();
                result.UsersUpdated++;
            }
            else
            {
                Log($"  User exists (skipping): {options.UserName}");
            }
        }

        // Assign roles
        if (options.Roles != null && options.Roles.Length > 0)
        {
            int rolesAdded = await AssignRolesToUserAsync(user, options.Roles);
            result.RoleAssignmentsAdded += rolesAdded;
        }
    }

    /// <summary>
    /// Assigns roles to a user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="roleNames">The role names to assign.</param>
    /// <returns>Number of role assignments added.</returns>
    private async Task<int> AssignRolesToUserAsync(
        NamedUser user,
        string[] roleNames)
    {
        // Get existing role assignments for this user
        HashSet<string> existingAssignments = (await _context.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Select(ur => ur.RoleId)
            .ToListAsync())
            .ToHashSet();

        // Get role IDs by name
        Dictionary<string, string> roleIdsByName = await _context.Roles
            .Where(r => roleNames
                .Select(n => n.ToUpperInvariant())
                .Contains(r.NormalizedName!))
            .ToDictionaryAsync(
                r => r.NormalizedName!,
                r => r.Id,
                StringComparer.OrdinalIgnoreCase);

        int added = 0;

        foreach (string roleName in roleNames)
        {
            if (!roleIdsByName.TryGetValue(
                roleName.ToUpperInvariant(),
                out string? roleId))
            {
                Log($"    Warning: Role '{roleName}' not found");
                continue;
            }

            if (!existingAssignments.Contains(roleId))
            {
                Log($"    Assigning role: {roleName}");

                _context.UserRoles.Add(new IdentityUserRole<string>
                {
                    UserId = user.Id,
                    RoleId = roleId
                });
                added++;
            }
        }

        if (added > 0)
            await _context.SaveChangesAsync();

        return added;
    }

    /// <summary>
    /// Seeds users into the database.
    /// </summary>
    /// <param name="users">The users to seed.</param>
    /// <returns>The seeding result.</returns>
    /// <exception cref="ArgumentNullException">users is null.</exception>
    public async Task<UserSeedResult> SeedAsync(
        IReadOnlyList<NamedSeededUserOptions> users)
    {
        ArgumentNullException.ThrowIfNull(users);

        UserSeedResult result = new();

        if (users.Count == 0)
        {
            Log("No users to seed.");
            return result;
        }

        // Validate users first
        List<string> validationErrors = ValidateUsers(users);
        if (validationErrors.Count > 0)
        {
            result.Errors.AddRange(validationErrors);
            return result;
        }

        // Ensure roles exist
        Log("Ensuring roles...");
        result.RolesCreated = await EnsureRolesAsync(users);
        Log($"  Roles created: {result.RolesCreated}");

        // Seed each user
        Log("Seeding users...");
        foreach (NamedSeededUserOptions userOptions in users)
        {
            try
            {
                await SeedUserAsync(userOptions, result);
                result.UsersProcessed++;
            }
            catch (Exception ex)
            {
                string error = $"Error seeding user '{userOptions.UserName}': " +
                    ex.Message;
                Log($"  [red]{error}[/]");
                result.Errors.Add(error);
            }
        }

        return result;
    }
}
