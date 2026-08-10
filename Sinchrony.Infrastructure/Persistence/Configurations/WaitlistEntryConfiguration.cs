using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class WaitlistEntryConfiguration : IEntityTypeConfiguration<WaitlistEntry>
{
    public void Configure(EntityTypeBuilder<WaitlistEntry> builder)
    {
        builder.ToTable("waitlist_entries");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Status).HasMaxLength(20);

        builder.HasOne(w => w.Class).WithMany()
            .HasForeignKey(w => w.ClassId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(w => w.Student).WithMany()
            .HasForeignKey(w => w.StudentId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(w => new { w.ClassId, w.StudentId }).IsUnique();
    }
}