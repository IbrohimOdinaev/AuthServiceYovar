using System.Security.Claims;
using System.Text.Json;
using AuthService.Application.Security;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace AuthService.Infrastructure.OpenIddict;

public sealed class ValidateRefreshTokenSessionHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ValidateTokenRequestContext>
{
    private readonly AuthDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ValidateRefreshTokenSessionHandler(
        AuthDbContext dbContext,
        IAuditEventWriter auditEventWriter,
        IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
        _httpContextAccessor = httpContextAccessor;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        if (!context.Request.IsRefreshTokenGrantType())
        {
            return;
        }

        var principal = context.RefreshTokenPrincipal;

        if (principal is null)
        {
            return;
        }

        var sessionIdValue = principal.FindFirst("sid")?.Value;

        if (!Guid.TryParse(sessionIdValue, out var sessionId))
        {
            await WriteRefreshTokenRejectedAuditAsync(
                context,
                principal,
                "InvalidSessionId",
                sessionIdValue);

            context.Reject(
                error: OpenIddictConstants.Errors.InvalidGrant,
                description: "The refresh token is not linked to a valid user session."
                );

            return;
        }

        var session = await _dbContext.UserSession
          .FirstOrDefaultAsync(session =>
              session.Id == sessionId &&
              session.RevokedAt == null,
              context.CancellationToken);

        if (session is null)
        {
            await WriteRefreshTokenRejectedAuditAsync(
                context,
                principal,
                "SessionNotActive",
                sessionId.ToString());

            context.Reject(
                error: OpenIddictConstants.Errors.InvalidGrant,
                description: "The user session associated with this refresh token is no longer active"
                );

            return;
        }

        session.Touch(DateTimeOffset.UtcNow);

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            GetUserId(principal),
            context.Request.ClientId,
            AuditEventTypes.RefreshTokenUsed,
            _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString(),
            _httpContextAccessor.HttpContext?.TraceIdentifier,
            JsonSerializer.Serialize(new
            {
                sessionId = session.Id
            })),
            context.CancellationToken);
    }

    private async Task WriteRefreshTokenRejectedAuditAsync(
        OpenIddictServerEvents.ValidateTokenRequestContext context,
        ClaimsPrincipal principal,
        string reason,
        string? sessionId)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        context.Transaction.SetProperty(
            OpenIddictAuditTransactionProperties.RefreshTokenRejectionAudited,
            "true");

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            GetUserId(principal),
            context.Request.ClientId,
            AuditEventTypes.RefreshTokenRejected,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString(),
            httpContext?.TraceIdentifier,
            JsonSerializer.Serialize(new
            {
                reason,
                sessionId
            })),
            context.CancellationToken);
    }

    private static Guid? GetUserId(ClaimsPrincipal principal)
    {
        var subject = principal.FindFirst(OpenIddictConstants.Claims.Subject)?.Value;

        return Guid.TryParse(subject, out var userId)
            ? userId
            : null;
    }
}
