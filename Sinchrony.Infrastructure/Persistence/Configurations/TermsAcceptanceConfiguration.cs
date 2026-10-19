using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class TermsAcceptanceConfiguration : IEntityTypeConfiguration<TermsAcceptance>
{
    public void Configure(EntityTypeBuilder<TermsAcceptance> builder)
    {
        builder.ToTable("terms_acceptances");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.TermsVersion).IsRequired().HasMaxLength(20);
        builder.Property(t => t.Channel).IsRequired().HasMaxLength(30);
        builder.Property(t => t.IpAddress).HasMaxLength(45);
        builder.Property(t => t.UserAgent).HasMaxLength(500);
        builder.Property(t => t.AppVersion).HasMaxLength(30);

        builder.HasOne(t => t.User).WithMany()
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.UserId, t.AcceptedAt });
    }
}
