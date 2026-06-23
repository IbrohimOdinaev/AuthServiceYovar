using AuthService.Infrastructure.Persistence;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace AuthService.Api.Health;

public sealed class AuthDbHealthCheck : IHealthCheck
{
    private readonly AuthDbContext _dbContext;

    public AuthDbHealthCheck(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);

        return canConnect
            ? HealthCheckResult.Healthy("Auth database is reachable.")
            : HealthCheckResult.Unhealthy("Auth database is not reachable.");
    }
}
