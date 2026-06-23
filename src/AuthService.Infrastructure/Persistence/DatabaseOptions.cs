namespace AuthService.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public int DbContextPoolSize { get; set; } = 128;

    public int CommandTimeoutSeconds { get; set; } = 30;

    public int MaxRetryCount { get; set; } = 3;

    public int MaxRetryDelaySeconds { get; set; } = 5;
}
