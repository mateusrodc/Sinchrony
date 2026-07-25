using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class TeacherUnitConfiguration : IEntityTypeConfiguration<TeacherUnit>
{
    public void Configure(EntityTypeBuilder<TeacherUnit> builder)
    {
        builder.ToTable("teacher_units");
        builder.HasKey(tu => new { tu.TeacherId, tu.UnitId });

        builder.HasOne(tu => tu.Teacher).WithMany(u => u.TeacherUnits)
            .HasForeignKey(tu => tu.TeacherId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(tu => tu.Unit).WithMany()
            .HasForeignKey(tu => tu.UnitId).OnDelete(DeleteBehavior.Cascade);
    }
}