using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Infrastructure.Persistence.Configurations
{
    public class PackageBenefitConfiguration : IEntityTypeConfiguration<PackageBenefit>
    {
        public void Configure(EntityTypeBuilder<PackageBenefit> builder)
        {
            builder.ToTable("package_benefits");
            builder.HasKey(pb => new { pb.PackageId, pb.BenefitId });

            builder.HasOne(pb => pb.Package).WithMany(p => p.PackageBenefits)
                .HasForeignKey(pb => pb.PackageId).OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pb => pb.Benefit).WithMany(b => b.PackageBenefits)
                .HasForeignKey(pb => pb.BenefitId).OnDelete(DeleteBehavior.Cascade);
        }
    }
}
