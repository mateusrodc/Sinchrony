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

            builder.Property(sp => sp.Source).HasMaxLength(20).HasDefaultValue("purchase");
            builder.Property(sp => sp.CreditsGranted).HasDefaultValue(0);
            builder.Property(sp => sp.AsaasSubscriptionId).HasMaxLength(50).IsRequired(false);

            builder.Property(sp => sp.PaymentStatus).HasConversion<string>().HasMaxLength(20);
            builder.Property(sp => sp.LastPaidAmount).HasPrecision(10, 2);
            builder.Property(sp => sp.LastFailureReason).HasMaxLength(500);
            builder.Property(sp => sp.AutoRenew).HasDefaultValue(false);
            builder.Property(sp => sp.RenewalAttempts).HasDefaultValue(0);
            builder.Property(sp => sp.RenewalPaidForCycle).HasDefaultValue(false);

            builder.HasIndex(sp => sp.AsaasSubscriptionId);

            builder.HasOne(sp => sp.Student).WithMany()
                .HasForeignKey(sp => sp.StudentId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(sp => sp.Package).WithMany()
                .HasForeignKey(sp => sp.PackageId).OnDelete(DeleteBehavior.Restrict);

            builder.HasOne<Card>().WithMany()
                .HasForeignKey(sp => sp.RenewalCardId).OnDelete(DeleteBehavior.SetNull);

            // Máximo 1 active + 1 queued por student — enforced na aplicação
            builder.HasIndex(sp => new { sp.StudentId, sp.Status });
        }
    }
}
