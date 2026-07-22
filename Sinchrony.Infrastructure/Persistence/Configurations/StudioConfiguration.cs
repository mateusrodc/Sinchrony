using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class StudioConfiguration : IEntityTypeConfiguration<Studio>
{
    public void Configure(EntityTypeBuilder<Studio> builder)
    {
        builder.ToTable("studios");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.Address).IsRequired().HasMaxLength(300);
        builder.Property(s => s.OpeningTime).HasMaxLength(5);
        builder.Property(s => s.ClosingTime).HasMaxLength(5);

        builder.Property(s => s.UnitId).IsRequired(false);
        builder.HasOne(s => s.Unit).WithMany(u => u.Studios)
            .HasForeignKey(s => s.UnitId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}