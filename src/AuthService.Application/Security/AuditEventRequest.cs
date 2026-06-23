namespace AuthService.Application.Security;

public sealed record AuditEventRequest(
    Guid? UserId,
    string? ClientId,
    string EventType,
    string? IpAddress,
    string? UserAgent,
    string? CorrelationId,
    string? MetadataJson
    );
