namespace AuthService.Domain.Security;

public sealed class LoginAttempt
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid? UserId { get; private set; }

    public string Identifier { get; private set; } = default!;

    public string? ClientId { get; private set; }

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string Result { get; private set; } = default!;

    public string? FailureReason { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private LoginAttempt()
    {
    }

    public LoginAttempt(
        Guid? userId,
        string identifier,
        string? clientId,
        string? ipAddress,
        string? userAgent,
        string result,
        string? failureReason,
        DateTimeOffset now)
    {
        UserId = userId;
        Identifier = identifier;
        ClientId = clientId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        Result = result;
        FailureReason = failureReason;
        CreatedAt = now;
    }
}
