using AuthService.Application.Security;
using AuthService.Infrastructure.OpenIddict;
using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Maintenance;
using OpenIddict.Server;
using AuthService.Infrastructure.Persistence;
using AuthService.Infrastructure.Security;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace AuthService.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var databaseOptions = configuration
            .GetSection("Database")
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        services
            .AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .Validate(options => options.DbContextPoolSize is >= 16 and <= 1024, "DbContext pool size must be between 16 and 1024.")
            .Validate(options => options.CommandTimeoutSeconds is >= 1 and <= 300, "Database command timeout must be between 1 and 300 seconds.")
            .Validate(options => options.MaxRetryCount is >= 0 and <= 10, "Database max retry count must be between 0 and 10.")
            .Validate(options => options.MaxRetryDelaySeconds is >= 1 and <= 60, "Database max retry delay must be between 1 and 60 seconds.")
            .ValidateOnStart();

        services
            .AddOptions<AuthDataRetentionOptions>()
            .Bind(configuration.GetSection("Maintenance:Retention"))
            .Validate(options => options.AuditEventsRetentionDays > 0, "Audit events retention must be greater than zero.")
            .Validate(options => options.LoginAttemptsRetentionDays > 0, "Login attempts retention must be greater than zero.")
            .Validate(options => options.RevokedSessionsRetentionDays > 0, "Revoked sessions retention must be greater than zero.")
            .Validate(options => options.CleanupBatchSize is >= 1 and <= 5000, "Cleanup batch size must be between 1 and 5000.")
            .Validate(options => options.CleanupIntervalMinutes > 0, "Cleanup interval must be greater than zero.")
            .ValidateOnStart();

        services.AddHostedService<AuthDataCleanupWorker>();

        services.AddDbContextPool<AuthDbContext>(options =>
        {
            options.UseNpgsql(
                configuration.GetConnectionString("AuthDb"),
                npgsqlOptions =>
                {
                    npgsqlOptions.CommandTimeout(databaseOptions.CommandTimeoutSeconds);
                    npgsqlOptions.EnableRetryOnFailure(
                        maxRetryCount: databaseOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(databaseOptions.MaxRetryDelaySeconds),
                        errorCodesToAdd: null);
                });

            options.UseOpenIddict();
        }, poolSize: databaseOptions.DbContextPoolSize);

        services.AddDataProtection()
            .SetApplicationName("AuthService")
            .PersistKeysToDbContext<AuthDbContext>();

        services
            .AddIdentity<AppUser, AppRole>(options =>
            {
                options.Password.RequiredLength = 12;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;

                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
                {
                    options.Cookie.Name = "__Host-auth";
                    options.Cookie.HttpOnly = true;
                    options.Cookie.Path = "/";
                    options.Cookie.SameSite = SameSiteMode.Lax;
                    options.Cookie.SecurePolicy = environment.IsDevelopment()
                        ? CookieSecurePolicy.SameAsRequest
                        : CookieSecurePolicy.Always;

                    options.LoginPath = "/account/login";
                    options.LogoutPath = "/account/logout";
                    options.AccessDeniedPath = "/account/access-denied";

                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<AuthDbContext>();
            })
            .AddServer(options =>
            {
                options.SetAuthorizationEndpointUris("/connect/authorize");
                options.SetTokenEndpointUris("/connect/token");
                options.SetUserInfoEndpointUris("/connect/userinfo");
                options.SetEndSessionEndpointUris("/connect/logout");

                options.AllowAuthorizationCodeFlow()
                    .AllowRefreshTokenFlow();

                options.SetAuthorizationCodeLifetime(TimeSpan.FromMinutes(5));
                options.SetAccessTokenLifetime(TimeSpan.FromMinutes(10));
                options.SetIdentityTokenLifetime(TimeSpan.FromMinutes(10));
                options.SetRefreshTokenLifetime(TimeSpan.FromDays(14));
                options.SetRefreshTokenReuseLeeway(TimeSpan.Zero);

                options.RequireProofKeyForCodeExchange();
                options.AddEventHandler<OpenIddictServerEvents.ValidateTokenRequestContext>(builder =>
                {
                    builder.UseScopedHandler<ValidateRefreshTokenSessionHandler>();
                });
                options.AddEventHandler<OpenIddictServerEvents.ApplyTokenResponseContext>(builder =>
                {
                    builder.UseScopedHandler<AuditRefreshTokenFailureHandler>();
                });
                options.Configure(serverOptions =>
                {
                    serverOptions.CodeChallengeMethods.Add("S256");
                });

                options.RegisterScopes(
                    "openid",
                    "profile",
                    "email",
                    "offline_access");

                options.AddAuthServiceCertificates(configuration, environment);
                options.DisableAccessTokenEncryption();

                var aspNetCore = options.UseAspNetCore()
                    .EnableAuthorizationEndpointPassthrough()
                    .EnableUserInfoEndpointPassthrough()
                    .EnableEndSessionEndpointPassthrough();

                if (environment.IsDevelopment())
                {
                    aspNetCore.DisableTransportSecurityRequirement();
                }
            })
            .AddValidation(options =>
                    {
                        options.UseLocalServer();

                        options.UseAspNetCore();
                    });

        services.AddScoped<OpenIddictSeeder>();
        services.AddScoped<IAuditEventWriter, AuditEventWriter>();
        services.AddHttpContextAccessor();


        return services;
    }
}
