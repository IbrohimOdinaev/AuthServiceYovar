namespace AuthService.Domain.Sessions;

public sealed class UserSession
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid UserId { get; private set; }
    public string ClientId { get; private set; } = default!;

    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset LastSeenAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public string? RevokedReason { get; private set; }

    public bool IsActive => RevokedAt is null;

    private UserSession()
    { }

    public UserSession(
            Guid userId,
            string clientId,
            string? ipAddress,
            string? userAgent,
            DateTimeOffset now)
    {
        UserId = userId;
        ClientId = clientId;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CreatedAt = now;
        LastSeenAt = now;
    }

    public void Touch(DateTimeOffset now)
    {
        if (!IsActive)
        {
            return;
        }

        LastSeenAt = now;
    }

    public void Revoke(DateTimeOffset now, string reason)
    {
        if (RevokedAt is not null)
        {
            return;
        }

        RevokedAt = now;
        RevokedReason = reason;
    }

}










