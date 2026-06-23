using AuthService.Api.Health;
using AuthService.Api.Options;
using AuthService.Infrastructure;
using AuthService.Infrastructure.OpenIddict;
using OpenIddict.Validation.AspNetCore;
using AuthService.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

var corsOptions = builder.Configuration
    .GetSection("Cors")
    .Get<CorsOptions>() ?? new CorsOptions();
var seedOpenIddictOnStartup = builder.Configuration.GetValue(
    "OpenIddict:SeedOnStartup",
    builder.Environment.IsDevelopment());

ValidateCorsOptions(corsOptions, builder.Environment.IsDevelopment());

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto |
        ForwardedHeaders.XForwardedHost;

    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

builder.Services.AddControllers();
builder.Services.AddRazorPages();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails(options =>
{
    options.CustomizeProblemDetails = context =>
    {
        context.ProblemDetails.Extensions["traceId"] =
            context.HttpContext.TraceIdentifier;
    };
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("trusted-clients", policy =>
    {
        policy.WithOrigins(corsOptions.AllowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.AddHealthChecks()
    .AddCheck("self", () => HealthCheckResult.Healthy(), tags: ["live"])
    .AddCheck<AuthDbHealthCheck>("auth-db", tags: ["ready"]);
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("auth-sensitive", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }
            ));
});
builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddAuthentication();
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(AuthPolicies.OrdersRead, policy =>
    {
        policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        policy.RequireAuthenticatedUser();
        policy.Requirements.Add(new ScopeRequirement(AuthScopes.OrdersRead));
    });
});

builder.Services.AddSingleton<IAuthorizationHandler, ScopeAuthorizationHandler>();

var app = builder.Build();

app.UseForwardedHeaders();

if (seedOpenIddictOnStartup)
{
    await using var scope = app.Services.CreateAsyncScope();

    var seeder = scope.ServiceProvider.GetRequiredService<OpenIddictSeeder>();
    await seeder.SeedAsync();
}
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler();
}

app.UseHttpsRedirection();
app.UseStatusCodePages();
app.UseRateLimiter();
app.UseCors("trusted-clients");

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new()
{
    Predicate = check => check.Tags.Contains("live")
});

app.MapHealthChecks("/health/ready", new()
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers()
    .RequireRateLimiting("auth-sensitive");

app.MapRazorPages()
    .RequireRateLimiting("auth-sensitive");

app.Run();

static void ValidateCorsOptions(CorsOptions options, bool isDevelopment)
{
    options.AllowedOrigins = options.AllowedOrigins
        .Where(origin => !string.IsNullOrWhiteSpace(origin))
        .Select(origin => origin.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    if (options.AllowedOrigins.Any(origin => origin == "*"))
    {
        throw new InvalidOperationException("Cors:AllowedOrigins cannot contain '*'. Configure explicit trusted origins.");
    }

    if (!isDevelopment && options.AllowedOrigins.Length == 0)
    {
        throw new InvalidOperationException("Cors:AllowedOrigins must contain at least one origin outside Development.");
    }

    foreach (var origin in options.AllowedOrigins)
    {
        if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"Cors origin '{origin}' is not a valid absolute URI.");
        }

        if (uri.Scheme is not "http" and not "https")
        {
            throw new InvalidOperationException($"Cors origin '{origin}' must use http or https.");
        }

        if (!isDevelopment && uri.Scheme != "https")
        {
            throw new InvalidOperationException($"Cors origin '{origin}' must use https outside Development.");
        }
    }
}
