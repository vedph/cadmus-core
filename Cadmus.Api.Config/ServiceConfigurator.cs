using Cadmus.Api.Services;
using Cadmus.Api.Services.Messaging;
using Cadmus.Core.Storage;
using Cadmus.Core;
using Cadmus.Graph.Ef.PgSql;
using Cadmus.Graph.Ef;
using Cadmus.Graph;
using Cadmus.Index.Config;
using Fusi.Api.Auth.Models;
using Fusi.DbManager.PgSql;
using MessagingApi;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using NpgsqlTypes;
using Serilog;
using Serilog.Debugging;
using Serilog.Sinks.PostgreSQL;
using Serilog.Sinks.PostgreSQL.ColumnWriters;
using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.RateLimiting;
using Fusi.Api.Auth.Services;
using System.Text.Json;
using ILogger = Microsoft.Extensions.Logging.ILogger;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Microsoft.Extensions.Hosting;
using System.Threading.Tasks;
using System.Collections.Generic;
using System;
using Cadmus.Api.Config.Services;
using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Cadmus.Graph.Extras;
using Cadmus.Export;
using Cadmus.Export.Config;

namespace Cadmus.Api.Config;

public static class ServiceConfigurator
{
    /// <summary>
    /// Dumps the environment variables in the log.
    /// </summary>
    public static void DumpEnvironmentVars()
    {
        Log.Information("ENVIRONMENT VARIABLES:");
        IDictionary dct = Environment.GetEnvironmentVariables();
        List<string> keys = [];
        var enumerator = dct.GetEnumerator();
        while (enumerator.MoveNext())
        {
            keys.Add(((DictionaryEntry)enumerator.Current).Key.ToString()!);
        }

        foreach (string key in keys.Order())
            Log.Information("{Key} = {Value}", key, dct[key]);
    }

    #region Options
    /// <summary>
    /// Configures the options services providing typed configuration objects.
    /// </summary>
    /// <param name="services">The services.</param>
    public static void ConfigureOptionsServices(IServiceCollection services,
        IConfiguration config)
    {
        // configuration sections
        // https://andrewlock.net/adding-validation-to-strongly-typed-configuration-objects-in-asp-net-core/
        services.Configure<MessagingOptions>(config.GetSection("Messaging"));
        services.Configure<DotNetMailerOptions>(config.GetSection("Mailer"));

        // explicitly register the settings object by delegating to the IOptions object
        services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<MessagingOptions>>().Value);
        services.AddSingleton(resolver =>
            resolver.GetRequiredService<IOptions<DotNetMailerOptions>>().Value);
    }
    #endregion

    #region CORS
    public static void ConfigureCorsServices(IServiceCollection services,
        IConfiguration config)
    {
        string[] origins = ["http://localhost:4200"];

        IConfigurationSection section = config.GetSection("AllowedOrigins");
        if (section.Exists())
        {
            origins = section.AsEnumerable()
                .Where(p => !string.IsNullOrEmpty(p.Value))
                .Select(p => p.Value).ToArray()!;
        }

        services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
        {
            builder.AllowAnyMethod()
                .AllowAnyHeader()
                // https://github.com/aspnet/SignalR/issues/2110 for AllowCredentials
                .AllowCredentials()
                .WithOrigins(origins);
        }));
    }
    #endregion

    #region Rate limiter
    public static async Task NotifyLimitExceededToRecipients(IConfiguration config,
        IHostEnvironment hostEnvironment)
    {
        // mailer must be enabled
        if (!config.GetValue<bool>("Mailer:IsEnabled"))
        {
            Log.Information("Mailer not enabled");
            return;
        }

        // recipients must be set
        IConfigurationSection recSection = config.GetSection("Mailer:Recipients");
        if (!recSection.Exists()) return;
        string[] recipients = recSection.AsEnumerable()
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => p.Value!).ToArray();
        if (recipients.Length == 0)
        {
            Log.Information("No recipients for limit notification");
            return;
        }

        // build message
        MessagingOptions msgOptions = new();
        config.GetSection("Messaging").Bind(msgOptions);
        FileMessageBuilderService messageBuilder = new(
            msgOptions,
            hostEnvironment);

        Message? message = messageBuilder.BuildMessage("rate-limit-exceeded",
            new Dictionary<string, string>()
            {
                ["EventTime"] = DateTime.UtcNow.ToString()
            });
        if (message == null)
        {
            Log.Warning("Unable to build limit notification message");
            return;
        }

        // send message to all the recipients
        DotNetMailerOptions mailerOptions = new();
        config.GetSection("Mailer").Bind(mailerOptions);
        DotNetMailerService mailer = new(mailerOptions);

        foreach (string recipient in recipients)
        {
            Log.Logger.Information("Sending rate email message");
            await mailer.SendEmailAsync(
                recipient,
                "Test Recipient",
                message);
            Log.Logger.Information("Email message sent");
        }
    }

    public static void ConfigureRateLimiterService(IServiceCollection services,
        IConfiguration config, IHostEnvironment hostEnvironment)
    {
        // nope if Disabled
        IConfigurationSection limit = config.GetSection("RateLimit");
        if (limit.GetValue("IsDisabled", false))
        {
            Log.Information("Rate limiter is disabled");
            return;
        }

        // PermitLimit (10)
        int permit = limit.GetValue("PermitLimit", 10);
        if (permit < 1) permit = 10;

        // QueueLimit (0)
        int queue = limit.GetValue("QueueLimit", 0);

        // TimeWindow (00:01:00 = HH:MM:SS)
        string? windowText = limit.GetValue<string>("TimeWindow");
        TimeSpan window;
        if (!string.IsNullOrEmpty(windowText))
        {
            if (!TimeSpan.TryParse(windowText, CultureInfo.InvariantCulture, out window))
                window = TimeSpan.FromMinutes(1);
        }
        else
        {
            window = TimeSpan.FromMinutes(1);
        }

        Log.Information("Configuring rate limiter: " +
            "limit={PermitLimit}, queue={QueueLimit}, window={Window}",
            permit, queue, window);

        // https://blog.maartenballiauw.be/post/2022/09/26/aspnet-core-rate-limiting-middleware.html
        // default = 10 requests per minute, per authenticated username,
        // or hostname if not authenticated.
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter
                .Create<HttpContext, string>(httpContext =>
                {
                    string key = httpContext.User.Identity?.Name
                        ?? httpContext.Connection.RemoteIpAddress?.ToString()
                        ?? "unknown";
                    Log.Information("Rate limit key: {Key}", key);

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: key,
                        factory: partition => new FixedWindowRateLimiterOptions
                        {
                            AutoReplenishment = true,
                            PermitLimit = permit,
                            QueueLimit = queue,
                            Window = window
                        });
                });

            options.OnRejected = async (context, token) =>
            {
                Log.Warning("Rate limit exceeded");

                // 429 too many requests
                context.HttpContext.Response.StatusCode = 429;

                // send
                await NotifyLimitExceededToRecipients(config, hostEnvironment);

                // ret JSON with error
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    await context.HttpContext.Response.WriteAsync("{\"error\": " +
                        "\"Too many requests. Please try again after " +
                        $"{retryAfter.TotalMinutes} minute(s).\"" +
                        "}", cancellationToken: token);
                }
                else
                {
                    await context.HttpContext.Response.WriteAsync(
                        "{\"error\": " +
                        "\"Too many requests. Please try again later.\"" +
                        "}", cancellationToken: token);
                }
            };
        });
    }
    #endregion

    #region Auth
    public static void ConfigureAuthServices(IServiceCollection services,
        IConfiguration config)
    {
        string csTemplate = config.GetConnectionString("Auth")!;
        string dbName = config.GetValue<string>("DatabaseNames:Auth")!;
        string cs = string.Format(CultureInfo.InvariantCulture, csTemplate, dbName);
        services.AddDbContext<ApplicationDbContext>(
            options => options.UseNpgsql(cs));

        services.AddIdentity<NamedUser, IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        // authentication service
        services
            .AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme =
                    JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme =
                    JwtBearerDefaults.AuthenticationScheme;
                options.DefaultScheme =
                    JwtBearerDefaults.AuthenticationScheme;
            })
        .AddJwtBearer(options =>
        {
            // NOTE: remember to set the values in configuration:
            // Jwt:SecureKey, Jwt:Audience, Jwt:Issuer
            IConfigurationSection jwtSection = config.GetSection("Jwt");
            string? key = jwtSection["SecureKey"];
            if (string.IsNullOrEmpty(key))
                throw new InvalidOperationException("Required JWT SecureKey not found");

            options.SaveToken = true;
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new TokenValidationParameters()
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidAudience = jwtSection["Audience"],
                ValidIssuer = jwtSection["Issuer"],
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(key))
            };

            // support refresh
            // https://stackoverflow.com/questions/55150099/jwt-token-expiration-time-failing-net-core
            options.Events = new JwtBearerEvents
            {
                OnAuthenticationFailed = context =>
                {
                    if (context.Exception.GetType() ==
                            typeof(SecurityTokenExpiredException))
                    {
                        context.Response.Headers.Append("Token-Expired", "true");
                    }
                    return Task.CompletedTask;
                }
            };
        });
#if DEBUG
        // use to show more information when troubleshooting JWT issues
        IdentityModelEventSource.ShowPII = true;
#endif
    }
    #endregion

    #region Preview
    public static CadmusPreviewer GetPreviewer(IServiceProvider provider,
        IConfiguration config)
    {
        // get dependencies
        ICadmusRepository repository =
                provider.GetService<IRepositoryProvider>()!.CreateRepository();
        StandardCadmusRenderingFactoryProvider factoryProvider = new();

        // nope if disabled
        if (!config.GetSection("Preview").GetSection("IsEnabled").Get<bool>())
        {
            return new CadmusPreviewer(factoryProvider.GetFactory("{}"),
                repository);
        }

        // get profile source
        ILogger? logger = provider.GetService<ILoggerFactory>()?
            .CreateLogger("CadmusApi.Program");
        IHostEnvironment env = provider.GetService<IHostEnvironment>()!;
        string path = Path.Combine(env.ContentRootPath,
            "wwwroot", "preview-profile.json");
        if (!File.Exists(path))
        {
            logger?.LogError("Preview profile expected at {Path} not found", path);
            return new CadmusPreviewer(factoryProvider.GetFactory("{}"),
                repository);
        }

        // load profile
        logger?.LogInformation("Loading preview profile from {Path}...", path);
        string profile;
        using (StreamReader reader = new(new FileStream(
            path, FileMode.Open, FileAccess.Read, FileShare.Read), Encoding.UTF8))
        {
            profile = reader.ReadToEnd();
        }
        CadmusRenderingFactory factory = factoryProvider.GetFactory(profile);

        return new CadmusPreviewer(factory, repository);
    }
    #endregion

    #region Index and graph
    /// <summary>
    /// Configures the item index services with the connection string template
    /// from <c>ConnectionStrings:Index</c>, whose database name is defined in
    /// <c>DatabaseNames:Data</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static void ConfigureIndexServices(IServiceCollection services,
        IConfiguration config)
    {
        // item index factory provider (from ConnectionStrings/Index)
        string cs = string.Format(config.GetConnectionString("Index")!,
            config.GetValue<string>("DatabaseNames:Data"));

        services.AddSingleton<IItemIndexFactoryProvider>(_ =>
            new StandardItemIndexFactoryProvider(cs));
    }

    /// <summary>
    /// Configures the item graph services with the connection string template
    /// from <c>ConnectionStrings:Graph</c> (falling back to <c>:Index</c> if
    /// not found), whose database name is defined in <c>DatabaseNames:Data</c>
    /// plus suffix <c>-graph</c>.
    /// </summary>
    /// <param name="services">The services.</param>
    public static void ConfigureGraphServices(IServiceCollection services,
        IConfiguration config)
    {
        string cs = string.Format(config.GetConnectionString("Graph")
            ?? config.GetConnectionString("Index")!,
            config.GetValue<string>("DatabaseNames:Data") + "-graph");

        services.AddSingleton<IItemGraphFactoryProvider>(_ =>
            new StandardItemGraphFactoryProvider(cs));

        services.AddSingleton<IGraphRepository>(_ =>
        {
            EfPgSqlGraphRepository repository = new();
            repository.Configure(new EfGraphRepositoryOptions
            {
                ConnectionString = cs
            });
            return repository;
        });

        // graph updater
        services.AddTransient<GraphUpdater>(provider =>
        {
            IRepositoryProvider rp = provider.GetService<IRepositoryProvider>()!;
            return new(provider.GetService<IGraphRepository>()!)
            {
                // we want item-eid as an additional metadatum, derived from
                // eid in the role-less MetadataPart of the item, when present
                MetadataSupplier = new MetadataSupplier()
                    .SetCadmusRepository(rp.CreateRepository())
                    .AddItemEid()
            };
        });
    }
    #endregion

    #region Messaging
    public static void ConfigureMessagingServices(IServiceCollection services)
    {
        // you can use another mailer service here. In this case,
        // also change the types in ConfigureOptionsServices.
        services.AddScoped<IMailerService, DotNetMailerService>();
        services.AddScoped<IMessageBuilderService,
            FileMessageBuilderService>();
    }
    #endregion

    #region Logging
    public static bool IsAuditEnabledFor(IConfiguration config, string key)
    {
        bool? value = config.GetValue<bool?>($"Auditing:{key}");
        return value != null && value != false;
    }

    public static void ConfigurePostgreLogging(HostBuilderContext context,
        LoggerConfiguration loggerConfiguration)
    {
        string? cs = context.Configuration.GetConnectionString("PostgresLog");
        if (string.IsNullOrEmpty(cs))
        {
            Console.WriteLine("Postgres log connection string not found");
            return;
        }

        Regex dbRegex = new("Database=(?<n>[^;]+);?");
        Match m = dbRegex.Match(cs);
        if (!m.Success)
        {
            Console.WriteLine("Postgres log connection string not valid");
            return;
        }
        string cst = dbRegex.Replace(cs, "Database={0};");
        string dbName = m.Groups["n"].Value;
        PgSqlDbManager mgr = new(cst);
        if (!mgr.Exists(dbName))
        {
            Console.WriteLine($"Creating log database {dbName}...");
            mgr.CreateDatabase(dbName, "", null);
        }

        IDictionary<string, ColumnWriterBase> columnWriters =
            new Dictionary<string, ColumnWriterBase>
        {
            { "message", new RenderedMessageColumnWriter(
                NpgsqlDbType.Text) },
            { "message_template", new MessageTemplateColumnWriter(
                NpgsqlDbType.Text) },
            { "level", new LevelColumnWriter(
                true, NpgsqlDbType.Varchar) },
            { "raise_date", new TimestampColumnWriter(
                NpgsqlDbType.TimestampTz) },
            { "exception", new ExceptionColumnWriter(
                NpgsqlDbType.Text) },
            { "properties", new LogEventSerializedColumnWriter(
                NpgsqlDbType.Jsonb) },
            { "props_test", new PropertiesColumnWriter(
                NpgsqlDbType.Jsonb) },
            { "machine_name", new SinglePropertyColumnWriter(
                "MachineName", PropertyWriteMethod.ToString,
                NpgsqlDbType.Text, "l") }
        };

        loggerConfiguration
            .WriteTo.PostgreSQL(cs, "log", columnWriters,
            needAutoCreateTable: true, needAutoCreateSchema: true);
    }

    public static void ConfigureLogger(WebApplicationBuilder builder)
    {
        // enable Serilog internal diagnostics so sink failures are not silent
        string selfLogPath = Path.Combine(
            AppContext.BaseDirectory, "serilog-selflog.txt");
        Console.WriteLine($"Serilog SelfLog: {selfLogPath}");
        SelfLog.Enable(msg => File.AppendAllText(
            selfLogPath,
            $"{DateTime.UtcNow:O} {msg}{Environment.NewLine}"));

        // pass all events through MEL; Serilog's own MinimumLevel handles filtering
        builder.Logging.SetMinimumLevel(LogLevel.Trace);

        builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
        {
            // https://github.com/serilog/serilog-settings-configuration
            loggerConfiguration
                .ReadFrom.Configuration(hostingContext.Configuration)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("MachineName", Environment.MachineName);

            if (IsAuditEnabledFor(hostingContext.Configuration, "File"))
            {
                Console.WriteLine("Logging to file enabled");
                loggerConfiguration.WriteTo.File("cadmus.log",
                    rollingInterval: RollingInterval.Day);
            }

            //if (IsAuditEnabledFor(hostingContext.Configuration, "Mongo"))
            //{
            //    Console.WriteLine("Logging to Mongo enabled");
            //    string? cs = hostingContext.Configuration
            //        .GetConnectionString("MongoLog");

            //    if (!string.IsNullOrEmpty(cs))
            //    {
            //        int maxSize = hostingContext.Configuration
            //            .GetValue<int>("Serilog:MaxMbSize");
            //        loggerConfiguration.WriteTo.MongoDBBson(cs,
            //            cappedMaxSizeMb: maxSize == 0 ? 10 : maxSize);
            //    }
            //    else
            //    {
            //        Console.WriteLine("Mongo log connection string not found");
            //    }
            //}

            if (IsAuditEnabledFor(hostingContext.Configuration, "Postgres"))
            {
                Console.WriteLine("Logging to Postgres enabled");
                ConfigurePostgreLogging(hostingContext, loggerConfiguration);
            }

            if (IsAuditEnabledFor(hostingContext.Configuration, "Console"))
            {
                Console.WriteLine("Logging to console enabled");
                loggerConfiguration.WriteTo.Console();
            }
        });
    }
    #endregion

    /// <summary>
    /// Configures all the infrastructural services except for Cadmus services,
    /// which depend on the parts used in the consumer API project.
    /// </summary>
    /// <param name="services">The services.</param>
    public static void ConfigureServices(IServiceCollection services,
        IConfiguration config, IHostEnvironment hostEnvironment)
    {
        // configuration
        services.AddSingleton(_ => config);
        ConfigureOptionsServices(services, config);

        // security
        ConfigureCorsServices(services, config);
        ConfigureRateLimiterService(services, config, hostEnvironment);
        ConfigureAuthServices(services, config);

        // proxy
        services.AddHttpClient();
        services.AddResponseCaching();

        // API controllers
        services.AddControllers();
        // camel-case JSON in response
        services.AddMvc()
            // https://docs.microsoft.com/en-us/aspnet/core/migration/22-to-30?view=aspnetcore-2.2&tabs=visual-studio#jsonnet-support
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase;
            });

        // framework services
        // IMemoryCache: https://docs.microsoft.com/en-us/aspnet/core/performance/caching/memory
        services.AddMemoryCache();

        // user repository service
        services.AddScoped<IUserRepository<NamedUser>,
            UserRepository<NamedUser, IdentityRole>>();

        // messaging
        ConfigureMessagingServices(services);
    }
}
