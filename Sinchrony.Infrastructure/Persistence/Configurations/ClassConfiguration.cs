using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class ClassConfiguration : IEntityTypeConfiguration<Class>
{
    public void Configure(EntityTypeBuilder<Class> builder)
    {
        builder.ToTable("classes");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.StartTime).HasMaxLength(5);
        builder.Property(c => c.EndTime).HasMaxLength(5);

        builder.HasOne(c => c.ClassType).WithMany(t => t.Classes)
            .HasForeignKey(c => c.ClassTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Teacher).WithMany()
            .HasForeignKey(c => c.TeacherId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Studio).WithMany(s => s.Classes)
            .HasForeignKey(c => c.StudioId).OnDelete(DeleteBehavior.Restrict);

        builder.Property(c => c.IsException).HasDefaultValue(false);

        builder.HasIndex(c => new { c.Date, c.StudioId });

        // Uma ocorrência por data original da série, mesmo depois de cancelada ou remarcada:
        // é isso que impede recriar a data ao estender a série. Aulas avulsas ficam fora (SeriesId nulo).
        builder.HasIndex(c => new { c.SeriesId, c.SeriesDate })
            .IsUnique()
            .HasFilter("\"SeriesId\" IS NOT NULL");
    }
}