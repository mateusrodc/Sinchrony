using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class TeacherBonusRateConfiguration : IEntityTypeConfiguration<TeacherBonusRate>
{
    public void Configure(EntityTypeBuilder<TeacherBonusRate> builder)
    {
        builder.ToTable("teacher_bonus_rates");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ValuePerStudent).HasPrecision(10, 2);
        builder.HasIndex(r => r.EffectiveFrom);
    }
}
