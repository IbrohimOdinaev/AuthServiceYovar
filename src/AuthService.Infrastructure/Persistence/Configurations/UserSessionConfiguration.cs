using AuthService.Domain.Sessions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Persistence.Configurations;

public sealed class UserSessionConfiguration : IEntityTypeConfiguration<UserSession>
{
    public void Configure(EntityTypeBuilder<UserSession> builder)
    {
        builder.ToTable("user_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.ClientId)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(session => session.IpAddress)
            .HasMaxLength(64);

        builder.Property(session => session.UserAgent)
            .HasMaxLength(512);

        builder.Property(session => session.RevokedReason)
            .HasMaxLength(200);

        builder.HasIndex(session => session.UserId);

        builder.HasIndex(session => session.ClientId);

        builder.HasIndex(session => session.RevokedAt);

        builder.HasIndex(session => new { session.UserId, session.LastSeenAt })
            .IsDescending(false, true);

        builder.HasIndex(session => new { session.UserId, session.RevokedAt });

        builder.HasIndex(session => new { session.UserId, session.ClientId, session.RevokedAt });
    }
}
