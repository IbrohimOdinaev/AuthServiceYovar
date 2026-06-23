namespace AuthService.Infrastructure.Maintenance;

public sealed class AuthDataRetentionOptions
{
    public bool Enabled { get; set; }

    public int AuditEventsRetentionDays { get; set; } = 180;

    public int LoginAttemptsRetentionDays { get; set; } = 90;

    public int RevokedSessionsRetentionDays { get; set; } = 90;

    public int CleanupBatchSize { get; set; } = 500;

    public int CleanupIntervalMinutes { get; set; } = 60;
}
