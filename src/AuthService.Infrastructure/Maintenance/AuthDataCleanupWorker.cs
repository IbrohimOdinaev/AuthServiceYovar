using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Infrastructure.Maintenance;

public sealed class AuthDataCleanupWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AuthDataRetentionOptions> _options;
    private readonly ILogger<AuthDataCleanupWorker> _logger;

    public AuthDataCleanupWorker(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<AuthDataRetentionOptions> options,
        ILogger<AuthDataCleanupWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.CurrentValue.Enabled)
        {
            _logger.LogInformation("Auth data cleanup worker is disabled.");
            return;
        }

        await CleanupOnceAsync(stoppingToken);

        using var timer = new PeriodicTimer(
            TimeSpan.FromMinutes(_options.CurrentValue.CleanupIntervalMinutes));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await CleanupOnceAsync(stoppingToken);
        }
    }

    private async Task CleanupOnceAsync(CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var now = DateTimeOffset.UtcNow;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();

        var auditEventsDeleted = await DeleteAuditEventsAsync(
            dbContext,
            now.AddDays(-options.AuditEventsRetentionDays),
            options.CleanupBatchSize,
            cancellationToken);

        var loginAttemptsDeleted = await DeleteLoginAttemptsAsync(
            dbContext,
            now.AddDays(-options.LoginAttemptsRetentionDays),
            options.CleanupBatchSize,
            cancellationToken);

        var revokedSessionsDeleted = await DeleteRevokedSessionsAsync(
            dbContext,
            now.AddDays(-options.RevokedSessionsRetentionDays),
            options.CleanupBatchSize,
            cancellationToken);

        if (auditEventsDeleted + loginAttemptsDeleted + revokedSessionsDeleted > 0)
        {
            _logger.LogInformation(
                "Auth data cleanup deleted {AuditEventsDeleted} audit events, {LoginAttemptsDeleted} login attempts and {RevokedSessionsDeleted} revoked sessions.",
                auditEventsDeleted,
                loginAttemptsDeleted,
                revokedSessionsDeleted);
        }
    }

    private static async Task<int> DeleteAuditEventsAsync(
        AuthDbContext dbContext,
        DateTimeOffset deleteBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.AuditEvent
            .Where(auditEvent => auditEvent.CreatedAt < deleteBefore)
            .OrderBy(auditEvent => auditEvent.CreatedAt)
            .Select(auditEvent => auditEvent.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        if (ids.Length == 0)
        {
            return 0;
        }

        return await dbContext.AuditEvent
            .Where(auditEvent => ids.Contains(auditEvent.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<int> DeleteLoginAttemptsAsync(
        AuthDbContext dbContext,
        DateTimeOffset deleteBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.LoginAttempt
            .Where(loginAttempt => loginAttempt.CreatedAt < deleteBefore)
            .OrderBy(loginAttempt => loginAttempt.CreatedAt)
            .Select(loginAttempt => loginAttempt.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        if (ids.Length == 0)
        {
            return 0;
        }

        return await dbContext.LoginAttempt
            .Where(loginAttempt => ids.Contains(loginAttempt.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<int> DeleteRevokedSessionsAsync(
        AuthDbContext dbContext,
        DateTimeOffset deleteBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var ids = await dbContext.UserSession
            .Where(session => session.RevokedAt != null && session.RevokedAt < deleteBefore)
            .OrderBy(session => session.RevokedAt)
            .Select(session => session.Id)
            .Take(batchSize)
            .ToArrayAsync(cancellationToken);

        if (ids.Length == 0)
        {
            return 0;
        }

        return await dbContext.UserSession
            .Where(session => ids.Contains(session.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }
}
