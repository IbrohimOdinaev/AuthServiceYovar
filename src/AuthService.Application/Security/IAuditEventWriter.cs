namespace AuthService.Application.Security;

public interface IAuditEventWriter
{
    Task WriteAsync(AuditEventRequest request, CancellationToken cancellationToken = default);
}
