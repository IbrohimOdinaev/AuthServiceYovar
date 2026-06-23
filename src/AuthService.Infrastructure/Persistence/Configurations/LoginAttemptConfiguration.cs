using AuthService.Domain.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Persistence.Configurations;

public sealed class LoginAttemptConfiguration : IEntityTypeConfiguration<LoginAttempt>
{
    public void Configure(EntityTypeBuilder<LoginAttempt> builder)
    {
        builder.ToTable("login_attempts");

        builder.HasKey(loginAttempt => loginAttempt.Id);

        builder.Property(loginAttempt => loginAttempt.Identifier)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(loginAttempt => loginAttempt.ClientId)
            .HasMaxLength(100);

        builder.Property(loginAttempt => loginAttempt.IpAddress)
            .HasMaxLength(64);

        builder.Property(loginAttempt => loginAttempt.UserAgent)
            .HasMaxLength(512);

        builder.Property(loginAttempt => loginAttempt.Result)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(loginAttempt => loginAttempt.FailureReason)
            .HasMaxLength(100);

        builder.HasIndex(loginAttempt => loginAttempt.UserId);

        builder.HasIndex(loginAttempt => loginAttempt.Identifier);

        builder.HasIndex(loginAttempt => loginAttempt.IpAddress);

        builder.HasIndex(loginAttempt => loginAttempt.CreatedAt);

        builder.HasIndex(loginAttempt => new { loginAttempt.Identifier, loginAttempt.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(loginAttempt => new { loginAttempt.IpAddress, loginAttempt.CreatedAt })
            .IsDescending(false, true);
    }
}
