using AuthService.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AuthService.Api.Controllers;

[ApiController]
[Route("account")]
public sealed class AccountController : ControllerBase
{
    private readonly UserManager<AppUser> _userManager;

    public AccountController(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(
        RegisterRequest request,
        CancellationToken cancellationToken
        )
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);

        if (existingUser is not null)
        {
            return Conflict(new
            {
                error = "user_already_exists"
            });
        }

        var user = new AppUser
        {
            UserName = request.Email,
            Email = request.Email,
            EmailConfirmed = true,
            FullName = request.FullName,
            Status = "Active",
            CreatedAt = DateTimeOffset.UtcNow
        };

        var result = await _userManager.CreateAsync(user, request.Password);

        if (!result.Succeeded)
        {
            return BadRequest(new
            {
                error = "registration_failed",
                details = result.Errors.Select(error => new
                {
                    error.Code,
                    error.Description
                })
            });
        }

        return Created($"/account/users/{user.Id}", new
        {
            user.Id,
            user.Email,
            user.FullName,
            user.Status
        });
    }
}

public sealed record RegisterRequest(
    string Email, string Password, string? FullName
    );
