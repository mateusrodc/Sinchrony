using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("user_permissions");
        builder.HasKey(up => up.Id);
        builder.HasIndex(up => new { up.UserId, up.PermissionId }).IsUnique();

        builder.HasOne(up => up.User).WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(up => up.Permission).WithMany()
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // GrantedByUserId aponta pra User também, mas sem navigation property dedicada —
        // é só rastro de auditoria de quem concedeu, não precisa navegar de volta.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(up => up.GrantedByUserId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);
    }
}
