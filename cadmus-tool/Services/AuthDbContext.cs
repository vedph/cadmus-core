using Cadmus.Cli.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Cadmus.Cli.Services;

/// <summary>
/// Database context for authentication using ASP.NET Core Identity
/// with PostgreSQL. This context maps to the custom Identity tables
/// created by the API's auth database initializer, using snake_case
/// naming conventions and custom table names (app_user, app_role, etc.).
/// </summary>
/// <seealso cref="IdentityDbContext{TUser, TRole, TKey}" />
public class AuthDbContext : IdentityDbContext<NamedUser, IdentityRole, string>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthDbContext"/> class.
    /// </summary>
    /// <param name="options">The options for this context.</param>
    public AuthDbContext(DbContextOptions<AuthDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Configures the database options, including snake_case naming convention.
    /// </summary>
    /// <param name="optionsBuilder">The options builder.</param>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSnakeCaseNamingConvention();

    /// <summary>
    /// Configures the model for the Identity entities with custom table names
    /// matching the PostgreSQL schema used by the API.
    /// </summary>
    /// <param name="builder">The model builder.</param>
    protected override void OnModelCreating(ModelBuilder builder)
    {
        // PostgreSQL uses the public schema by default
        builder.HasDefaultSchema("public");

        base.OnModelCreating(builder);

        // Configure NamedUser with custom table name and additional properties
        builder.Entity<NamedUser>(entity =>
        {
            entity.ToTable("app_user");
            entity.Property(u => u.FirstName).HasMaxLength(100);
            entity.Property(u => u.LastName).HasMaxLength(100);
        });

        // Configure Identity tables with custom names
        builder.Entity<IdentityUserClaim<string>>(entity =>
        {
            entity.ToTable("app_user_claim");
        });

        builder.Entity<IdentityUserLogin<string>>(entity =>
        {
            entity.ToTable("app_user_login");
        });

        builder.Entity<IdentityUserToken<string>>(entity =>
        {
            entity.ToTable("app_user_token");
        });

        builder.Entity<IdentityRole>(entity =>
        {
            entity.ToTable("app_role");
        });

        builder.Entity<IdentityRoleClaim<string>>(entity =>
        {
            entity.ToTable("app_role_claim");
        });

        builder.Entity<IdentityUserRole<string>>(entity =>
        {
            entity.ToTable("app_user_role");
        });
    }
}
