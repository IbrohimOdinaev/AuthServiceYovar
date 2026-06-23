using AuthService.Infrastructure.Identity;
using AuthService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("account/audit-events")]
[Authorize]
public sealed class AuditEventsController : ControllerBase
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 200;

    private readonly UserManager<AppUser> _userManager;
    private readonly AuthDbContext _dbContext;

    public AuditEventsController(
        UserManager<AppUser> userManager,
        AuthDbContext dbContext)
    {
        _userManager = userManager;
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<IActionResult> GetAuditEvents([FromQuery] int limit = DefaultLimit)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
        {
            return Unauthorized();
        }

        var normalizedLimit = Math.Clamp(limit, 1, MaxLimit);

        var events = await _dbContext.AuditEvent
            .Where(auditEvent => auditEvent.UserId == user.Id)
            .OrderByDescending(auditEvent => auditEvent.CreatedAt)
            .Take(normalizedLimit)
            .Select(auditEvent => new
            {
                auditEvent.Id,
                auditEvent.EventType,
                auditEvent.ClientId,
                auditEvent.IpAddress,
                auditEvent.UserAgent,
                auditEvent.CorrelationId,
                auditEvent.MetadataJson,
                auditEvent.CreatedAt
            })
            .ToListAsync();

        return Ok(events);
    }
}
