using Microsoft.Extensions.Configuration;
using System;
using Microsoft.AspNetCore.Identity;
using Fusi.Api.Auth.Models;
using Fusi.Api.Auth.Services;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Reflection;

namespace Cadmus.Api.Services.Seeding;

/// <summary>
/// Application's user accounts database initializer.
/// </summary>
public sealed class AuthDatabaseInitializer :
    AuthDatabaseInitializer<NamedUser, IdentityRole, NamedSeededUserOptions>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AuthDatabaseInitializer"/>
    /// class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public AuthDatabaseInitializer(IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
    }

    /// <summary>
    /// Initializes the user.
    /// </summary>
    /// <param name="user">The user.</param>
    /// <param name="options">The options.</param>
    protected override void InitUser(NamedUser user,
        NamedSeededUserOptions options)
    {
        base.InitUser(user, options);

        user.FirstName = options.FirstName;
        user.LastName = options.LastName;
    }

    /// <summary>
    /// Initializes the database.
    /// </summary>
    protected override void InitDatabase()
    {
        DatabaseTarget target = CadmusDatabaseConfig.GetAuthDatabase(
            Configuration);
        Logger.LogInformation("Ensuring auth database {Database}...", target);

        string schema;
        using (StreamReader reader = new(
            Assembly.GetExecutingAssembly().GetManifestResourceStream(
                "Cadmus.Api.Services.Assets.Auth.pgsql")!))
        {
            schema = reader.ReadToEnd();
        }

        PgSqlDatabaseInitializer.EnsureDatabase(target, schema, null, Logger);
    }
}
