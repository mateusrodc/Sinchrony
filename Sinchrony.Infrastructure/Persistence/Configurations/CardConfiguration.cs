using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Sinchrony.Domain.Entities;

namespace Sinchrony.Infrastructure.Persistence.Configurations;

public class CardConfiguration : IEntityTypeConfiguration<Card>
{
    public void Configure(EntityTypeBuilder<Card> builder)
    {
        builder.ToTable("cards");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.LastDigits).IsRequired().HasMaxLength(4);
        builder.Property(c => c.Brand).IsRequired().HasMaxLength(20);
        builder.Property(c => c.HolderName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.ExpiryDate).IsRequired().HasMaxLength(5);
        builder.Property(c => c.Token).IsRequired().HasMaxLength(200);
        builder.Property(c => c.Nickname).HasMaxLength(50);
    }
}