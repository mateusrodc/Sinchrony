using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class PackageConfiguration : IEntityTypeConfiguration<Package>
{
    public void Configure(EntityTypeBuilder<Package> builder)
    {
        builder.ToTable("packages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Name).IsRequired().HasMaxLength(100);
        builder.Property(p => p.Price).HasPrecision(10, 2);
        builder.Property(p => p.PricePerCredit).HasPrecision(10, 2);

        builder.Property(p => p.PurchaseStrategy).HasMaxLength(30).HasDefaultValue("block");
        builder.Property(p => p.NoShowCreditPenalty).HasDefaultValue(true);
        builder.Property(p => p.NoShowBlockWindowDays).HasDefaultValue(30);

        builder.Property(p => p.PackageTypeId).IsRequired(false);

        builder.HasOne(p => p.PackageType).WithMany(pt => pt.Packages)
            .HasForeignKey(p => p.PackageTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);
    }
}