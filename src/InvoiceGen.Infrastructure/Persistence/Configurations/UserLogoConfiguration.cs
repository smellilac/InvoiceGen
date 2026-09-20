using InvoiceGen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceGen.Infrastructure.Persistence.Configurations;

public sealed class UserLogoConfiguration : IEntityTypeConfiguration<UserLogo>
{
    public void Configure(EntityTypeBuilder<UserLogo> builder)
    {
        builder.ToTable("user_logos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageBytes).IsRequired(); // Postgres bytea
        builder.Property(x => x.ContentType).IsRequired().HasMaxLength(100);

        // One logo per user; the FK cascades so deleting a user removes their logo.
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
