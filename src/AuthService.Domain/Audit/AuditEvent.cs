namespace AuthService.Domain.Audit;

public sealed class AuditEvent
{
    public Guid Id { get; private set; } = Guid.NewGuid();

    public Guid? UserId { get; private set; }

    public string? ClientId { get; private set; }

    public string EventType { get; private set; } = default!;

    public string? IpAddress { get; private set; }

    public string? UserAgent { get; private set; }

    public string? CorrelationId { get; private set; }

    public string? MetadataJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid? userId,
        string? clientId,
        string eventType,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        string? metadataJson,
        DateTimeOffset now)
    {
        UserId = userId;
        ClientId = clientId;
        EventType = eventType;
        IpAddress = ipAddress;
        UserAgent = userAgent;
        CorrelationId = correlationId;
        MetadataJson = metadataJson;
        CreatedAt = now;
    }
}
