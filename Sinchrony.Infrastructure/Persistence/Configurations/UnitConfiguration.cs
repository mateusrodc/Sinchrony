using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        builder.Property(u => u.Address).HasMaxLength(300);
        builder.Property(u => u.Phone).HasMaxLength(20);
        builder.Property(u => u.Email).HasMaxLength(100);
        builder.Property(u => u.Cep).HasMaxLength(8);
        builder.Property(u => u.Logradouro).HasMaxLength(200);
        builder.Property(u => u.Numero).HasMaxLength(20);
        builder.Property(u => u.Complemento).HasMaxLength(100);
        builder.Property(u => u.Bairro).HasMaxLength(100);
        builder.Property(u => u.Cidade).HasMaxLength(100);
        builder.Property(u => u.Estado).HasMaxLength(2);
    }
}