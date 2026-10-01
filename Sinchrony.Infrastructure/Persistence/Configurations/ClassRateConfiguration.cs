using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class ClassRateConfiguration : IEntityTypeConfiguration<ClassRate>
{
    public void Configure(EntityTypeBuilder<ClassRate> builder)
    {
        builder.ToTable("class_rates");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Value).HasPrecision(10, 2);

        builder.HasOne(r => r.ClassType).WithMany()
            .HasForeignKey(r => r.ClassTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Resolução do valor vigente: por modalidade, maior EffectiveFrom <= data da aula
        builder.HasIndex(r => new { r.ClassTypeId, r.EffectiveFrom });
    }
}
