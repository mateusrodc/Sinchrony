using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Infrastructure.Persistence.Configurations
{
    public class StudentPackageConfiguration : IEntityTypeConfiguration<StudentPackage>
    {
        public void Configure(EntityTypeBuilder<StudentPackage> builder)
        {
            builder.ToTable("student_packages");
            builder.HasKey(sp => sp.Id);
            builder.Property(sp => sp.Status).HasConversion<string>().HasMaxLength(20);

            builder.HasOne(sp => sp.Student).WithMany()
                .HasForeignKey(sp => sp.StudentId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(sp => sp.Package).WithMany()
                .HasForeignKey(sp => sp.PackageId).OnDelete(DeleteBehavior.Restrict);

            // Máximo 1 active + 1 queued por student — enforced na aplicação
            builder.HasIndex(sp => new { sp.StudentId, sp.Status });
        }
    }
}
