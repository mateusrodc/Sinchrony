using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class BikeConfiguration : IEntityTypeConfiguration<Bike>
{
    public void Configure(EntityTypeBuilder<Bike> builder)
    {
        builder.ToTable("bikes");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(b => b.Notes).HasMaxLength(500);

        builder.HasOne(b => b.Studio).WithMany(s => s.Bikes)
            .HasForeignKey(b => b.StudioId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(b => new { b.StudioId, b.Number }).IsUnique();
    }
}