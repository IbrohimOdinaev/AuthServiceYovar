using AuthService.Domain.Audit;
using AuthService.Domain.Security;
using AuthService.Domain.Sessions;
using AuthService.Infrastructure.Identity;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Infrastructure.Persistence;

public sealed class AuthDbContext
    : IdentityDbContext<AppUser, AppRole, Guid>, IDataProtectionKeyContext
{
    public DbSet<UserSession> UserSession => Set<UserSession>();
    public DbSet<AuditEvent> AuditEvent => Set<AuditEvent>();
    public DbSet<LoginAttempt> LoginAttempt => Set<LoginAttempt>();
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    public AuthDbContext(DbContextOptions<AuthDbContext> options)
      : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.UseOpenIddict();

        builder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
