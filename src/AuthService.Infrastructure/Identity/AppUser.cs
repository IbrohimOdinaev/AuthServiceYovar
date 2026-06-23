using Microsoft.AspNetCore.Identity;

namespace AuthService.Infrastructure.Identity;

public sealed class AppUser : IdentityUser<Guid>
{
    public string? FullName { get; set; }
    public string Status { get; set; } = "Active";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
