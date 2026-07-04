using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.ToTable("attendance_records");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(a => a.Class).WithMany()
            .HasForeignKey(a => a.ClassId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Student).WithMany()
            .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.ConfirmedBy).WithMany()
            .HasForeignKey(a => a.ConfirmedById).OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(a => a.Booking).WithMany()
            .HasForeignKey(a => a.BookingId).OnDelete(DeleteBehavior.Restrict);
    }
}