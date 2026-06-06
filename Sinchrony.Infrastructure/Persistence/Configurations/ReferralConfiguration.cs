using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class ReferralConfiguration : IEntityTypeConfiguration<Referral>
{
    public void Configure(EntityTypeBuilder<Referral> builder)
    {
        builder.ToTable("referrals");
        builder.HasKey(r => r.Id);

        builder.HasOne(r => r.Referrer).WithMany()
            .HasForeignKey(r => r.ReferrerId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.Referred).WithMany()
            .HasForeignKey(r => r.ReferredId).OnDelete(DeleteBehavior.Restrict);
    }
}