using AuthService.Application.Security;
using AuthService.Domain.Audit;
using AuthService.Infrastructure.Persistence;

namespace AuthService.Infrastructure.Security;

public sealed class AuditEventWriter : IAuditEventWriter
{
    private readonly AuthDbContext _dbContext;

    public AuditEventWriter(AuthDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync(
        AuditEventRequest request,
        CancellationToken cancellationToken = default
        )
    {
        var auditEvent = new AuditEvent(
            request.UserId,
            request.ClientId,
            request.EventType,
            request.IpAddress,
            request.UserAgent,
            request.CorrelationId,
            request.MetadataJson,
            DateTimeOffset.UtcNow
            );

        _dbContext.AuditEvent.Add(auditEvent);

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
