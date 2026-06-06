using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class BookingConfiguration : IEntityTypeConfiguration<Booking>
{
    public void Configure(EntityTypeBuilder<Booking> builder)
    {
        builder.ToTable("bookings");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(b => b.Class).WithMany(c => c.Bookings)
            .HasForeignKey(b => b.ClassId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.Student).WithMany(u => u.Bookings)
            .HasForeignKey(b => b.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(b => b.AttendanceRecord)
            .WithOne(ar => ar.Booking)
            .HasForeignKey<AttendanceRecord>(ar => ar.BookingId);

        // Aspas duplas no filtro para respeitar o case-sensitive do PostgreSQL
        builder.HasIndex(b => new { b.StudentId, b.ClassId })
            .IsUnique()
            .HasFilter("\"Status\" NOT IN ('cancelled')");
    }
}