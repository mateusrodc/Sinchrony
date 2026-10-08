using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class ClassSeriesConfiguration : IEntityTypeConfiguration<ClassSeries>
{
    public void Configure(EntityTypeBuilder<ClassSeries> builder)
    {
        builder.ToTable("class_series");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name).IsRequired().HasMaxLength(100);
        builder.Property(s => s.StartTime).IsRequired().HasMaxLength(5);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Ignore(s => s.DaysOfWeek);

        builder.HasOne(s => s.ClassType).WithMany()
            .HasForeignKey(s => s.ClassTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Teacher).WithMany()
            .HasForeignKey(s => s.TeacherId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(s => s.Studio).WithMany()
            .HasForeignKey(s => s.StudioId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(s => s.Occurrences).WithOne(c => c.Series)
            .HasForeignKey(c => c.SeriesId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(s => s.RequestId).IsUnique();
    }
}
