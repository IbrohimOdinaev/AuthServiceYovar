using System.Reflection;
using AuthService.Application.Security;
using AuthService.Domain.Audit;
using AuthService.Domain.Security;
using AuthService.Domain.Sessions;
using AuthService.Infrastructure.Maintenance;
using AuthService.Infrastructure.Security;
using AuthService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Options;

namespace AuthService.Tests;

public class AuthEventWriterTests
{
    [Fact]
    public async Task WriteAsync_persists_audit_event_in_inmemory_db()
    {
        await using var dbContext = await CreateDbContextAsync();
        var writer = new AuditEventWriter(dbContext);

        await writer.WriteAsync(new AuditEventRequest(
            UserId: Guid.NewGuid(),
            ClientId: "client-1",
            EventType: "login.success",
            IpAddress: "127.0.0.1",
            UserAgent: "test-agent",
            CorrelationId: "corr-1",
            MetadataJson: "{}"));

        var count = await dbContext.AuditEvent.CountAsync();
        var saved = await dbContext.AuditEvent.SingleAsync();

        Assert.Equal(1, count);
        Assert.Equal("login.success", saved.EventType);
        Assert.Equal("corr-1", saved.CorrelationId);
        Assert.NotEqual(default, saved.Id);
    }

    private static async Task<AuthDbContext> CreateDbContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new AuthDbContext(options);
        await dbContext.Database.EnsureCreatedAsync();

        return dbContext;
    }
}

public class AuthDataCleanupWorkerTests
{
    private const string RequiresPostgresSkipReason =
        "Requires PostgreSQL provider because cleanup queries use DateTimeOffset comparisons with ExecuteDeleteAsync.";

    [Fact]
    public async Task ExecuteAsync_returns_without_cleanup_when_disabled()
    {
        var options = new AuthDataRetentionOptions { Enabled = false };
        var optionsMonitor = new Mock<IOptionsMonitor<AuthDataRetentionOptions>>();
        optionsMonitor.Setup(x => x.CurrentValue).Returns(options);

        var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
        var loggerMock = new Mock<ILogger<AuthDataCleanupWorker>>(MockBehavior.Loose);

        var worker = new AuthDataCleanupWorker(
            scopeFactoryMock.Object,
            optionsMonitor.Object,
            loggerMock.Object);

        var executeAsync = typeof(AuthDataCleanupWorker).GetMethod(
            "ExecuteAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        await ((Task)executeAsync.Invoke(worker, [CancellationToken.None])!);

        scopeFactoryMock.VerifyNoOtherCalls();
    }

    [Fact(Skip = RequiresPostgresSkipReason)]
    public async Task DeleteAuditEventsAsync_deletes_only_too_old_entries_up_to_batch_size()
    {
        await using var dbContext = await CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;

        dbContext.AuditEvent.AddRange([
            new AuditEvent(Guid.NewGuid(), "client", "old-1", "ip", "ua", "c1", "{}", now.AddDays(-40)),
            new AuditEvent(Guid.NewGuid(), "client", "old-2", "ip", "ua", "c2", "{}", now.AddDays(-30)),
            new AuditEvent(Guid.NewGuid(), "client", "older", "ip", "ua", "c3", "{}", now.AddDays(-20)),
            new AuditEvent(Guid.NewGuid(), "client", "recent", "ip", "ua", "c4", "{}", now.AddDays(-5))
        ]);
        await dbContext.SaveChangesAsync();

        var deleted = await InvokeCleanupTask(
            dbContext,
            "DeleteAuditEventsAsync",
            now.AddDays(-25),
            2,
            CancellationToken.None);

        Assert.Equal(2, deleted);

        var remainingEvents = await dbContext.AuditEvent.OrderBy(x => x.EventType).ToArrayAsync();
        var remainingTypes = string.Join(",", remainingEvents.Select(x => x.EventType));
        Assert.Contains("older", remainingTypes);
        Assert.Contains("recent", remainingTypes);
        Assert.DoesNotContain("old-1", remainingTypes);
        Assert.DoesNotContain("old-2", remainingTypes);
    }

    [Fact(Skip = RequiresPostgresSkipReason)]
    public async Task DeleteLoginAttemptsAsync_deletes_too_old_login_attempts_up_to_batch_size()
    {
        await using var dbContext = await CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;

        dbContext.LoginAttempt.AddRange([
            new LoginAttempt(
                Guid.NewGuid(),
                "user1",
                "client",
                "ip",
                "agent",
                "failed",
                "bad_password",
                now.AddDays(-20)),
            new LoginAttempt(
                Guid.NewGuid(),
                "user2",
                "client",
                "ip",
                "agent",
                "failed",
                "bad_password",
                now.AddDays(-15)),
            new LoginAttempt(
                Guid.NewGuid(),
                "user3",
                "client",
                "ip",
                "agent",
                "failed",
                "bad_password",
                now.AddDays(-3))
        ]);
        await dbContext.SaveChangesAsync();

        var deleted = await InvokeCleanupTask(
            dbContext,
            "DeleteLoginAttemptsAsync",
            now.AddDays(-10),
            10,
            CancellationToken.None);

        Assert.Equal(2, deleted);
        Assert.Equal(1, await dbContext.LoginAttempt.CountAsync());
        Assert.Equal("user3", (await dbContext.LoginAttempt.SingleAsync()).Identifier);
    }

    [Fact(Skip = RequiresPostgresSkipReason)]
    public async Task DeleteRevokedSessionsAsync_deletes_only_old_revoked_sessions()
    {
        await using var dbContext = await CreateDbContextAsync();
        var now = DateTimeOffset.UtcNow;

        var activeSession = new UserSession(Guid.NewGuid(), "client", "ip", "agent", now.AddDays(-30));
        var revokedOldSession = new UserSession(Guid.NewGuid(), "client", "ip", "agent", now.AddDays(-30));
        revokedOldSession.Revoke(now.AddDays(-20), "manual");
        var revokedRecentSession = new UserSession(Guid.NewGuid(), "client", "ip", "agent", now.AddDays(-5));
        revokedRecentSession.Revoke(now.AddDays(-1), "manual");

        dbContext.UserSession.AddRange([activeSession, revokedOldSession, revokedRecentSession]);
        await dbContext.SaveChangesAsync();

        var deleted = await InvokeCleanupTask(
            dbContext,
            "DeleteRevokedSessionsAsync",
            now.AddDays(-10),
            10,
            CancellationToken.None);

        Assert.Equal(1, deleted);
        Assert.Equal(2, await dbContext.UserSession.CountAsync());
        Assert.All(await dbContext.UserSession.ToListAsync(), session =>
            Assert.NotEqual(revokedOldSession.Id, session.Id));
    }

    private static Task<AuthDbContext> CreateDbContextAsync()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new AuthDbContext(options);
        dbContext.Database.EnsureCreated();

        return Task.FromResult(dbContext);
    }

    private static async Task<int> InvokeCleanupTask(
        AuthDbContext dbContext,
        string methodName,
        DateTimeOffset deleteBefore,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var method = typeof(AuthDataCleanupWorker).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var task = method?.Invoke(
            null,
            [dbContext, deleteBefore, batchSize, cancellationToken]) as Task<int>;

        Assert.NotNull(task);
        return await task!;
    }
}
