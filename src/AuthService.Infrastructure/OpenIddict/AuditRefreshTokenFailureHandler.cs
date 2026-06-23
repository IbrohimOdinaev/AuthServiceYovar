using System.Text.Json;
using AuthService.Application.Security;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace AuthService.Infrastructure.OpenIddict;

public sealed class AuditRefreshTokenFailureHandler : IOpenIddictServerHandler<OpenIddictServerEvents.ApplyTokenResponseContext>
{
    private readonly IAuditEventWriter _auditEventWriter;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuditRefreshTokenFailureHandler(
        IAuditEventWriter auditEventWriter,
        IHttpContextAccessor httpContextAccessor)
    {
        _auditEventWriter = auditEventWriter;
        _httpContextAccessor = httpContextAccessor;
    }

    public async ValueTask HandleAsync(OpenIddictServerEvents.ApplyTokenResponseContext context)
    {
        if (context.Request is null || !context.Request.IsRefreshTokenGrantType())
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(context.Error))
        {
            return;
        }

        if (context.Transaction.GetProperty<string>(
                OpenIddictAuditTransactionProperties.RefreshTokenRejectionAudited) == "true")
        {
            return;
        }

        var httpContext = _httpContextAccessor.HttpContext;

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            null,
            context.Request.ClientId,
            AuditEventTypes.RefreshTokenRejected,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString(),
            httpContext?.TraceIdentifier,
            JsonSerializer.Serialize(new
            {
                reason = "TokenEndpointRejected",
                error = context.Error,
                errorDescription = context.Response?.ErrorDescription
            })),
            context.CancellationToken);
    }
}
