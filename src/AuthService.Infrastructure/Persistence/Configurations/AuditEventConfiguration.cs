using AuthService.Domain.Audit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AuthService.Infrastructure.Persistence.Configurations;

public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");

        builder.HasKey(auditEvent => auditEvent.Id);

        builder.Property(auditEvent => auditEvent.ClientId)
            .HasMaxLength(100);

        builder.Property(auditEvent => auditEvent.EventType)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(auditEvent => auditEvent.IpAddress)
            .HasMaxLength(64);

        builder.Property(auditEvent => auditEvent.UserAgent)
            .HasMaxLength(512);

        builder.Property(auditEvent => auditEvent.CorrelationId)
            .HasMaxLength(100);

        builder.Property(auditEvent => auditEvent.MetadataJson)
            .HasColumnType("jsonb");

        builder.HasIndex(auditEvent => auditEvent.UserId);

        builder.HasIndex(auditEvent => auditEvent.ClientId);

        builder.HasIndex(auditEvent => auditEvent.EventType);

        builder.HasIndex(auditEvent => auditEvent.CreatedAt);

        builder.HasIndex(auditEvent => new { auditEvent.UserId, auditEvent.CreatedAt })
            .IsDescending(false, true);

        builder.HasIndex(auditEvent => new { auditEvent.EventType, auditEvent.CreatedAt })
            .IsDescending(false, true);
    }
}
