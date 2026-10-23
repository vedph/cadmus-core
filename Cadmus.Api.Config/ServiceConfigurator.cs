using Cadmus.Api.Services;
using Cadmus.Api.Services.Messaging;
using Cadmus.Core.Storage;
using Cadmus.Core;
using Cadmus.Graph.Ef.PgSql;
using Cadmus.Graph.Ef;
using Cadmus.Graph;
using Cadmus.Index.Config;
using Fusi.Api.Auth.Controllers;
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
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using System.Threading;
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
    /// <summary>
    /// Configures the CORS services with the allowed origins defined in
    /// the configuration under <c>AllowedOrigins</c>. If no origins are defined,
    /// the default is <c>http://localhost:4200</c>. If the origin is <c>*</c>,
    /// any origin is allowed.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="config">The configuration.</param>
    public static void ConfigureCorsServices(IServiceCollection services,
        IConfiguration config)
    {
        string[] origins = ["http://localhost:4200"];

        IConfigurationSection section = config.GetSection("AllowedOrigins");
        if (section.Exists())
        {
            origins = [.. section.AsEnumerable()
                    .Where(p => !string.IsNullOrWhiteSpace(p.Value))
                    .Select(p => p.Value!.Trim())
                    .Distinct()];
        }

        // a "*" origin means any origin: the CORS protocol forbids
        // combining a wildcard origin with credentials, so in this case
        // we allow any origin without credentials
        bool anyOrigin = origins.Contains("*");

        services.AddCors(o => o.AddPolicy("CorsPolicy", builder =>
        {
            builder.AllowAnyMethod().AllowAnyHeader();

            if (anyOrigin)
            {
                builder.AllowAnyOrigin();
            }
            else
            {
                // https://github.com/aspnet/SignalR/issues/2110 for AllowCredentials
                builder.AllowCredentials().WithOrigins(origins);
            }
        }));
    }
    #endregion

    #region Forwarded headers
    /// <summary>
    /// Gets the forwarded headers options for use with a reverse proxy.
    /// This lets the API (and its rate limiter) see the client's IP address
    /// and the original scheme (e.g. https) rather than the proxy's ones.
    /// The <c>X-Forwarded-*</c> headers are honored only when the request
    /// comes from a trusted proxy, i.e. (besides localhost, which is always
    /// trusted) one of the proxies listed in <c>ForwardedHeaders:KnownProxies</c>
    /// (IP addresses) or belonging to the networks listed in
    /// <c>ForwardedHeaders:KnownNetworks</c> (CIDR, e.g. <c>172.16.0.0/12</c>).
    /// Setting <c>ForwardedHeaders:TrustAllProxies</c> to true trusts any
    /// sender: use it only when the API port cannot be reached except via
    /// the proxy, as otherwise anyone can fake its IP address.
    /// <c>ForwardedHeaders:ForwardLimit</c> (default 1) is the number of
    /// proxies in front of the API.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>Options.</returns>
    public static ForwardedHeadersOptions GetForwardedHeadersOptions(
        IConfiguration config)
    {
        IConfigurationSection section = config.GetSection("ForwardedHeaders");

        ForwardedHeadersOptions options = new()
        {
            ForwardedHeaders = ForwardedHeaders.XForwardedFor
                | ForwardedHeaders.XForwardedProto,
            ForwardLimit = Math.Max(1, section.GetValue("ForwardLimit", 1))
        };

        if (section.GetValue("TrustAllProxies", false))
        {
            Log.Warning("Forwarded headers: trusting all proxies");
            options.KnownIPNetworks.Clear();
            options.KnownProxies.Clear();
            return options;
        }

        foreach (string proxy in section.GetSection("KnownProxies")
            .Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(proxy, out IPAddress? address))
                options.KnownProxies.Add(address);
            else
                Log.Warning("Invalid known proxy address: {Proxy}", proxy);
        }
        foreach (string network in section.GetSection("KnownNetworks")
            .Get<string[]>() ?? [])
        {
            if (System.Net.IPNetwork.TryParse(network,
                out System.Net.IPNetwork net))
            {
                options.KnownIPNetworks.Add(net);
            }
            else
            {
                Log.Warning("Invalid known network: {Network}", network);
            }
        }

        Log.Information("Forwarded headers: trusted proxies={Proxies}, " +
            "networks={Networks}, limit={Limit}",
            string.Join(", ", options.KnownProxies),
            string.Join(", ", options.KnownIPNetworks),
            options.ForwardLimit);

        return options;
    }

    /// <summary>
    /// Adds the forwarded headers middleware configured from
    /// <c>ForwardedHeaders</c> (see <see cref="GetForwardedHeadersOptions"/>).
    /// Call this first in the pipeline, so that the client IP address and
    /// scheme are restored before anything else (e.g. the rate limiter,
    /// HTTPS redirection, or the OpenAPI servers URL) uses them.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="config">The configuration.</param>
    public static void UseForwardedHeaders(IApplicationBuilder app,
        IConfiguration config)
    {
        app.UseForwardedHeaders(GetForwardedHeadersOptions(config));
    }
    #endregion

    #region Rate limiter
    // the minimum interval between two rate limit exceeded notifications
    private static readonly TimeSpan _limitNotificationInterval =
        TimeSpan.FromMinutes(30);
    private static long _lastLimitNotificationTicks;

    /// <summary>
    /// Determines whether a rate limit exceeded notification should be
    /// sent, i.e. whether no other notification was sent in the last
    /// <see cref="_limitNotificationInterval"/>. This avoids sending a
    /// message for each rejected request.
    /// </summary>
    /// <returns>True to notify.</returns>
    private static bool ShouldNotifyLimitExceeded()
    {
        long now = DateTime.UtcNow.Ticks;
        long last = Interlocked.Read(ref _lastLimitNotificationTicks);
        if (now - last < _limitNotificationInterval.Ticks) return false;
        return Interlocked.CompareExchange(
            ref _lastLimitNotificationTicks, now, last) == last;
    }

    /// <summary>
    /// Gets the rate limiter partition key for the specified request: this
    /// is the authenticated user name, or the client IP address for anonymous
    /// requests. The IP address is correct only when the forwarded headers
    /// are configured for an eventual reverse proxy (see
    /// <see cref="GetForwardedHeadersOptions"/>).
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>Key.</returns>
    public static string GetRateLimitKey(HttpContext context)
    {
        if (context.User.Identity?.IsAuthenticated == true
            && !string.IsNullOrEmpty(context.User.Identity.Name))
        {
            return "user:" + context.User.Identity.Name;
        }

        IPAddress? address = context.Connection.RemoteIpAddress;
        if (address == null) return "ip:unknown";
        // the same client may appear as IPv4 or IPv4-mapped IPv6
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        return "ip:" + address;
    }

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

    /// <summary>
    /// Configures the rate limiter services from the <c>RateLimit</c>
    /// configuration section, unless <c>RateLimit:IsDisabled</c> is true.
    /// Each client (the authenticated user name, or the IP address for
    /// anonymous requests) gets <c>PermitLimit</c> requests (default 10)
    /// in each <c>TimeWindow</c> (default 00:01:00); up to <c>QueueLimit</c>
    /// requests (default 0) over the limit are queued, the others get 429.
    /// Remember to add the middleware with <see cref="UseRateLimiter"/>, and
    /// to configure the forwarded headers when behind a reverse proxy, or
    /// all the anonymous clients will share the proxy's IP address quota.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="config">The configuration.</param>
    /// <param name="hostEnvironment">The host environment.</param>
    public static void ConfigureRateLimiterService(IServiceCollection services,
        IConfiguration config, IHostEnvironment hostEnvironment)
    {
        // nope if Disabled
        IConfigurationSection limit = config.GetSection("RateLimit");
        if (IsRateLimiterDisabled(config))
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
        // or client IP address if not authenticated.
        services.AddRateLimiter(options =>
        {
            options.GlobalLimiter = PartitionedRateLimiter
                .Create<HttpContext, string>(httpContext =>
                {
                    string key = GetRateLimitKey(httpContext);
                    Log.Debug("Rate limit key: {Key}", key);

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
                Log.Warning("Rate limit exceeded by {Key}: {Path}",
                    GetRateLimitKey(context.HttpContext),
                    context.HttpContext.Request.Path);

                // 429 too many requests
                context.HttpContext.Response.StatusCode = 429;
                context.HttpContext.Response.ContentType = "application/json";

                // send (throttled, as a burst of requests can be rejected)
                if (ShouldNotifyLimitExceeded())
                    await NotifyLimitExceededToRecipients(config, hostEnvironment);

                // ret JSON with error
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter,
                    out var retryAfter))
                {
                    context.HttpContext.Response.Headers.RetryAfter =
                        ((int)Math.Ceiling(retryAfter.TotalSeconds))
                        .ToString(CultureInfo.InvariantCulture);
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

    /// <summary>
    /// Determines whether the rate limiter is disabled via
    /// <c>RateLimit:IsDisabled</c>.
    /// </summary>
    /// <param name="config">The configuration.</param>
    /// <returns>True if disabled.</returns>
    public static bool IsRateLimiterDisabled(IConfiguration config) =>
        config.GetValue("RateLimit:IsDisabled", false);

    /// <summary>
    /// Adds the rate limiter middleware unless disabled (see
    /// <see cref="ConfigureRateLimiterService"/>). Call this after
    /// <c>UseAuthentication</c>, so that authenticated users are limited
    /// by their user name rather than by their IP address, and before
    /// <c>UseAuthorization</c>.
    /// </summary>
    /// <param name="app">The application builder.</param>
    /// <param name="config">The configuration.</param>
    public static void UseRateLimiter(IApplicationBuilder app,
        IConfiguration config)
    {
        if (!IsRateLimiterDisabled(config)) app.UseRateLimiter();
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

        // JWT authentication from the Jwt section: Jwt:SecureKey (at least
        // 32 chars), Jwt:Issuer, Jwt:Audience, Jwt:ExpirationMinutes
        services.AddJwtBearerAuthentication(config);
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
