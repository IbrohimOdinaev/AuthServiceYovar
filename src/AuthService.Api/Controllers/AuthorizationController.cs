using System.Security.Claims;
using System.Text.Json;
using AuthService.Application.Security;
using AuthService.Infrastructure.Identity;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Collections.Immutable;
using AuthService.Domain.Sessions;
using AuthService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Api.Controllers;

public sealed class AuthorizationController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public AuthorizationController(
        UserManager<AppUser> userManager,
        AuthDbContext dbContext,
        IAuditEventWriter auditEventWriter)
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        if (request is null)
        {
            return BadRequest();
        }

        if (User.Identity?.IsAuthenticated != true)
        {
            var returnUrl = Request.PathBase + Request.Path + QueryString.Create(
                Request.HasFormContentType
                  ? Request.Form.ToList()
                  : Request.Query.ToList()
                );

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties
                {
                    RedirectUri = returnUrl
                }
                );
        }

        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Forbid(authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        var session = CreateUserSession(user, request);

        _dbContext.UserSession.Add(session);

        await _dbContext.SaveChangesAsync();

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            user.Id,
            session.ClientId,
            AuditEventTypes.UserSessionCreated,
            session.IpAddress,
            session.UserAgent,
            HttpContext.TraceIdentifier,
            JsonSerializer.Serialize(new
            {
                sessionId = session.Id,
                scopes = request.GetScopes()
            })));

        var principal = await CreateClaimsPrincipalAsync(user, request, session);

        return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    public async Task<IActionResult> Logout()
    {
        var request = HttpContext.GetOpenIddictServerRequest();

        var user = await _userManager.GetUserAsync(User);

        if (user is not null)
        {
            var clientId = request?.ClientId;

            var sessionQuery = _dbContext.UserSession
                .Where(session =>
                        session.UserId == user.Id &&
                        session.RevokedAt == null);

            if (!string.IsNullOrWhiteSpace(clientId))
            {
                sessionQuery = sessionQuery.Where(session => session.ClientId == clientId);
            }

            var sessions = await sessionQuery.ToListAsync();

            var now = DateTimeOffset.UtcNow;

            foreach (var session in sessions)
            {
                session.Revoke(now, "Logout");
            }

            await _dbContext.SaveChangesAsync();

            await _auditEventWriter.WriteAsync(new AuditEventRequest(
                        user.Id,
                        clientId,
                        AuditEventTypes.UserSessionRevoked,
                        HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Request.Headers.UserAgent.ToString(),
                        HttpContext.TraceIdentifier,
                        JsonSerializer.Serialize(new
                        {
                            reason = "Logout",
                            revokedCount = sessions.Count
                        })
                        ));
        }
        return SignOut(
                authenticationSchemes:
                [
                    IdentityConstants.ApplicationScheme,
                        OpenIddictServerAspNetCoreDefaults.AuthenticationScheme
                ]
                );


    }

    private async Task<ClaimsPrincipal> CreateClaimsPrincipalAsync(
        AppUser user, OpenIddictRequest request, UserSession session
        )
    {
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            OpenIddictConstants.Claims.Name,
            OpenIddictConstants.Claims.Role
            );

        identity.AddClaim(OpenIddictConstants.Claims.Subject, await _userManager.GetUserIdAsync(user));
        identity.AddClaim("sid", session.Id.ToString());

        if (!string.IsNullOrWhiteSpace(user.Email))
        {
            identity.AddClaim(OpenIddictConstants.Claims.Email, user.Email);
        }

        if (!string.IsNullOrWhiteSpace(user.FullName))
        {
            identity.AddClaim(OpenIddictConstants.Claims.Name, user.FullName);
        }

        var roles = await _userManager.GetRolesAsync(user);

        foreach (var role in roles)
        {
            identity.AddClaim(OpenIddictConstants.Claims.Role, role);
        }

        var principal = new ClaimsPrincipal(identity);

        principal.SetScopes(request.GetScopes());

        principal.SetResources(GetResources(request.GetScopes()));

        foreach (var claim in principal.Claims)
        {
            claim.SetDestinations(GetDestinations(claim));
        }

        return principal;
    }

    private static IEnumerable<string> GetResources(ImmutableArray<string> scopes)
    {
        if (scopes.Contains("orders.read") || scopes.Contains("orders.write"))
        {
            yield return "orders-api";
        }

        if (scopes.Contains("users.read") || scopes.Contains("users.manage"))
        {
            yield return "users-api";
        }
    }

    private static IEnumerable<string> GetDestinations(Claim claim)
    {
        return claim.Type switch
        {
            OpenIddictConstants.Claims.Subject => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Email => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name => [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Role => [OpenIddictConstants.Destinations.AccessToken],
            "sid" => [OpenIddictConstants.Destinations.AccessToken],

            _ => [OpenIddictConstants.Destinations.AccessToken]
        };

    }

    private UserSession CreateUserSession(AppUser user, OpenIddictRequest request)
    {
        var now = DateTimeOffset.UtcNow;

        var clientId = request.ClientId;

        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = "unknown";
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        return new UserSession(
                user.Id,
                clientId,
                ipAddress,
                userAgent,
                now
                );
    }


}
