using Fusi.Api.Auth.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Cadmus.Api.Config.Services;

/// <summary>
/// Base class for authentication database initializers.
/// </summary>
/// <typeparam name="TUser">The type of the user.</typeparam>
/// <typeparam name="TRole">The type of the role.</typeparam>
/// <typeparam name="TSeededUserOptions">The type of the seeded user options.
/// </typeparam>
public abstract class AuthDatabaseInitializer<TUser, TRole, TSeededUserOptions>
    : IAuthDatabaseInitializer
    where TUser : IdentityUser
    where TRole : IdentityRole
    where TSeededUserOptions : SeededUserOptions
{
    private readonly UserManager<TUser> _userManager;
    private readonly RoleManager<TRole> _roleManager;
    private readonly TSeededUserOptions[] _seededUsersOptions;

    /// <summary>
    /// Gets the configuration.
    /// </summary>
    protected IConfiguration Configuration { get; }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger { get; }

    /// <summary>
    /// Initializes a new instance of the
    /// <see cref="AuthDatabaseInitializer{TUser, TRole, TSeededUserOptions}"/>
    /// class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <exception cref="ArgumentNullException">Null provider.</exception>
    protected AuthDatabaseInitializer(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);

        Configuration = serviceProvider.GetService<IConfiguration>()!;

        ILoggerFactory loggerFactory = serviceProvider.GetService<ILoggerFactory>()!;
        Logger = loggerFactory.CreateLogger<AuthDatabaseInitializer
            <TUser, TRole, TSeededUserOptions>>();

        _userManager = serviceProvider.GetService<UserManager<TUser>>()!;
        _roleManager = serviceProvider.GetService<RoleManager<TRole>>()!;

        // if StockUsers configuration entry exists:
        if (Configuration.GetSection("StockUsers") != null)
        {
            _seededUsersOptions = Configuration
                .GetSection("StockUsers")
                // requires Microsoft.Extensions.Configuration.Binder
                .Get<TSeededUserOptions[]>() ?? [];
        }
        else
        {
            _seededUsersOptions = [];
        }
    }

    /// <summary>
    /// Initializes the database, creating it if not existing, or otherwise
    /// preparing it before the seed process.
    /// </summary>
    protected abstract void InitDatabase();

    private async Task SeedRoles()
    {
        foreach (TSeededUserOptions options in _seededUsersOptions
            .Where(o => o.Roles != null))
        {
            foreach (string roleName in options.Roles!)
            {
                // add role if not existing
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    TRole r = Activator.CreateInstance<TRole>();
                    r.Name = roleName;
                    await _roleManager.CreateAsync(r);
                }
            }
        }
    }

    /// <summary>
    /// Initializes the user with the specified options. Override this
    /// to initialize additional properties, but ensure to call this base
    /// implementation unless you are going to provide also the
    /// <see cref="IdentityUser"/> "stock" properties.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="options">The options.</param>
    protected virtual void InitUser(TUser user, TSeededUserOptions options)
    {
        user.UserName = options.UserName;
        user.Email = options.Email;
        // email is automatically confirmed for a stock user
        user.EmailConfirmed = true;
        user.SecurityStamp = Guid.NewGuid().ToString();
    }

    private async Task SeedUserAsync(TSeededUserOptions options)
    {
        TUser? user = await _userManager.FindByNameAsync(options.UserName!);

        if (user == null)
        {
            user = Activator.CreateInstance<TUser>();
            InitUser(user, options);

            IdentityResult result =
                await _userManager.CreateAsync(user, options.Password!);
            if (!result.Succeeded)
            {
                Logger.LogError("Error creating user {UserName}: {Error}",
                    user.UserName, result.ToString());
                return;
            }
            user = await _userManager.FindByNameAsync(options.UserName!);
        }
        // ensure that user is automatically confirmed
        if (!user!.EmailConfirmed)
        {
            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);
        }

        if (options.Roles != null)
        {
            foreach (string role in options.Roles)
            {
                if (!await _userManager.IsInRoleAsync(user, role))
                    await _userManager.AddToRoleAsync(user, role);
            }
        }
    }

    /// <summary>
    /// Seeds the users with their roles.
    /// </summary>
    protected async Task SeedUsersWithRoles()
    {
        // roles
        await SeedRoles();

        // users
        if (_seededUsersOptions != null)
        {
            foreach (TSeededUserOptions options in _seededUsersOptions)
                await SeedUserAsync(options);
        }
    }

    /// <summary>
    /// Seeds the database. This calls <see cref="InitDatabase"/> and then
    /// <see cref="SeedUsersWithRoles"/>. You can override this method for
    /// additional data.
    /// </summary>
    public async virtual Task SeedAsync()
    {
        InitDatabase();
        await SeedUsersWithRoles();
    }
}
