using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace Sinchrony.Infrastructure.Persistence.Configurations
{
    public class DependentPackageAllocationConfiguration : IEntityTypeConfiguration<DependentPackageAllocation>
    {
        public void Configure(EntityTypeBuilder<DependentPackageAllocation> builder)
        {
            builder.ToTable("dependent_package_allocations");
            builder.HasKey(a => a.Id);

            builder.HasOne(a => a.StudentPackage).WithMany(sp => sp.Allocations)
                .HasForeignKey(a => a.StudentPackageId).OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.Dependent).WithMany()
                .HasForeignKey(a => a.DependentId).OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        }
    }
}
