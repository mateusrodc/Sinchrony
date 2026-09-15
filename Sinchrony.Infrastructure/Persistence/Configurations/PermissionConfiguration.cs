using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Resource).IsRequired().HasMaxLength(50);
        builder.Property(p => p.Action).IsRequired().HasMaxLength(20);
        builder.HasIndex(p => new { p.Resource, p.Action }).IsUnique();
    }
}
