using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class PurchaseConfiguration : IEntityTypeConfiguration<Purchase>
{
    public void Configure(EntityTypeBuilder<Purchase> builder)
    {
        builder.ToTable("purchases");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.PaymentMethod).IsRequired().HasMaxLength(10);
        builder.Property(p => p.Status).IsRequired().HasMaxLength(20);
        builder.Property(p => p.Kind).IsRequired().HasMaxLength(20).HasDefaultValue("purchase");

        // Listagem do ERP filtra por período e ordena por data decrescente
        builder.HasIndex(p => p.CreatedAt);

        builder.HasOne(p => p.User).WithMany(u => u.Purchases)
            .HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Package).WithMany(pk => pk.Purchases)
            .HasForeignKey(p => p.PackageId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Coupon).WithMany()
            .HasForeignKey(p => p.CouponId).OnDelete(DeleteBehavior.SetNull);

        // Histórico de pagamentos de uma assinatura recorrente (payments[] no contrato da API).
        builder.HasOne(p => p.StudentPackage).WithMany()
            .HasForeignKey(p => p.StudentPackageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(p => p.StudentPackageId);
    }
}