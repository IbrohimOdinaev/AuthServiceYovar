using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AuthService.Application.Security;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("account/sessions")]
[Authorize]
public sealed class SessionController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;
    private readonly AuthDbContext _dbContext;
    private readonly IAuditEventWriter _auditEventWriter;

    public SessionController(
        UserManager<AppUser> userManager,
        AuthDbContext dbContext,
        IAuditEventWriter auditEventWriter
        )
    {
        _userManager = userManager;
        _dbContext = dbContext;
        _auditEventWriter = auditEventWriter;
    }

    [HttpGet]
    public async Task<IActionResult> GetSessions()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var sessions = await _dbContext.UserSession
          .Where(session => session.UserId == user.Id)
          .OrderByDescending(session => session.LastSeenAt)
          .Select(session => new
          {
              session.Id,
              session.ClientId,
              session.IpAddress,
              session.UserAgent,
              session.CreatedAt,
              session.LastSeenAt,
              session.RevokedAt,
              session.IsActive
          })
          .ToListAsync();


        return Ok(sessions);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> RevokeSession(Guid id)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var session = await _dbContext.UserSession
            .FirstOrDefaultAsync(session =>
                    session.Id == id &&
                    session.UserId == user.Id);

        if (session is null)
        {
            return NotFound();
        }

        session.Revoke(DateTimeOffset.UtcNow, "RevokedByUser");

        await _dbContext.SaveChangesAsync();

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            user.Id,
            session.ClientId,
            AuditEventTypes.UserSessionRevoked,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            HttpContext.TraceIdentifier,
            $$"""{"sessionId":"{{session.Id}}"}"""));

        return NoContent();
    }

    [HttpPost("revoke-all")]
    public async Task<IActionResult> RevokeAllSessions()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var activeSessions = await _dbContext.UserSession
            .Where(session =>
                session.UserId == user.Id &&
                session.RevokedAt == null)
            .ToListAsync();

        var now = DateTimeOffset.UtcNow;

        foreach (var session in activeSessions)
        {
            session.Revoke(now, "RevokedAllByUser");
        }

        await _dbContext.SaveChangesAsync();

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            user.Id,
            null,
            AuditEventTypes.AllUserSessionsRevoked,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            HttpContext.TraceIdentifier,
            $$"""{"revokedCount":{{activeSessions.Count}}}"""));

        return NoContent();
    }
}
