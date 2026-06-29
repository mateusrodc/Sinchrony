using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class SettingsConfiguration : IEntityTypeConfiguration<Settings>
{
    public void Configure(EntityTypeBuilder<Settings> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.StudioName).IsRequired().HasMaxLength(100);
        builder.Property(s => s.StudioEmail).IsRequired().HasMaxLength(200);
        builder.Property(s => s.StudioPhone).HasMaxLength(20);
        builder.Property(s => s.StudioAddress).HasMaxLength(300);
        builder.Property(s => s.SmtpHost).HasMaxLength(200);
        builder.Property(s => s.SmtpUser).HasMaxLength(200);
        builder.Property(s => s.SmtpPassword).HasMaxLength(500);
        builder.Property(s => s.SmtpFrom).HasMaxLength(200);
    }
}