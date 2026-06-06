using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Event).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Entity).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Details).HasColumnType("text");
        builder.Property(a => a.IpAddress).HasMaxLength(45);

        builder.HasIndex(a => a.Event);
        builder.HasIndex(a => a.UserId);
        builder.HasIndex(a => a.CreatedAt);
    }
}