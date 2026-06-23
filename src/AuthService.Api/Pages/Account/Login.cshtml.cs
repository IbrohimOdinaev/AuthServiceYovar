using System.Text.Json;
using AuthService.Application.Security;
using AuthService.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;

namespace AuthService.Api.Pages.Account;

public sealed class LoginModel : PageModel
{
    private readonly SignInManager<AppUser> _signInManager;
    private readonly UserManager<AppUser> _userManager;
    private readonly IAuditEventWriter _auditEventWriter;

    public LoginModel(
        SignInManager<AppUser> signInManager,
        UserManager<AppUser> userManager,
        IAuditEventWriter auditEventWriter)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _auditEventWriter = auditEventWriter;
    }

    [BindProperty]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public IActionResult OnGet(string? returnUrl = null)
    {
        ReturnUrl = returnUrl;

        if (User.Identity?.IsAuthenticated == true)
        {
            return Redirect(ReturnUrl ?? "/");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await _userManager.FindByEmailAsync(Email);
        var clientId = GetClientIdFromReturnUrl();
        var metadataJson = JsonSerializer.Serialize(new
        {
            identifier = Email
        });

        var result = await _signInManager.PasswordSignInAsync(
            Email,
            Password,
            isPersistent: false,
            lockoutOnFailure: true
            );

        if (!result.Succeeded)
        {
            await _auditEventWriter.WriteAsync(new AuditEventRequest(
                user?.Id,
                clientId,
                AuditEventTypes.LoginFailed,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                Request.Headers.UserAgent.ToString(),
                HttpContext.TraceIdentifier,
                metadataJson));

            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return Page();
        }

        await _auditEventWriter.WriteAsync(new AuditEventRequest(
            user?.Id,
            clientId,
            AuditEventTypes.LoginSucceeded,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            HttpContext.TraceIdentifier,
            metadataJson));

        return Redirect(ReturnUrl ?? "/");
    }

    private string? GetClientIdFromReturnUrl()
    {
        if (string.IsNullOrWhiteSpace(ReturnUrl))
        {
            return null;
        }

        var queryStartIndex = ReturnUrl.IndexOf('?');

        if (queryStartIndex < 0)
        {
            return null;
        }

        var query = QueryHelpers.ParseQuery(ReturnUrl[queryStartIndex..]);

        return query.TryGetValue("client_id", out var clientId)
            ? clientId.ToString()
            : null;
    }
}
