using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class AdminAlertConfiguration : IEntityTypeConfiguration<AdminAlert>
{
    public void Configure(EntityTypeBuilder<AdminAlert> builder)
    {
        builder.ToTable("admin_alerts");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Type).IsRequired().HasMaxLength(30);
        builder.Property(a => a.TransactionId).IsRequired().HasMaxLength(100);
        builder.Property(a => a.Amount).HasPrecision(10, 2);

        // Chave de idempotência: reprocessar o mesmo evento não duplica o alerta.
        builder.HasIndex(a => a.TransactionId).IsUnique();
        builder.HasIndex(a => a.ReadAt);

        builder.HasOne<User>().WithMany()
            .HasForeignKey(a => a.StudentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<StudentPackage>().WithMany()
            .HasForeignKey(a => a.StudentPackageId).OnDelete(DeleteBehavior.Restrict);
    }
}
