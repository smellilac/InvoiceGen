using InvoiceGen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceGen.Infrastructure.Persistence.Configurations;

public sealed class DocumentConfiguration : IEntityTypeConfiguration<Document>
{
    public void Configure(EntityTypeBuilder<Document> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        builder.Property(x => x.Number).HasMaxLength(40);
        builder.Property(x => x.RelatedDocumentNumber).HasMaxLength(40);
        builder.Property(x => x.From).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.To).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.Currency).IsRequired().HasMaxLength(10);
        builder.Property(x => x.Notes).HasMaxLength(2000);
        builder.Property(x => x.Terms).HasMaxLength(2000);

        builder.Property(x => x.TaxPercent).HasPrecision(18, 2);
        builder.Property(x => x.DiscountPercent).HasPrecision(18, 2);
        builder.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Property(x => x.AmountSettled).HasPrecision(18, 2);
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.TaxAmount).HasPrecision(18, 2);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.Property(x => x.BalanceRemaining).HasPrecision(18, 2);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(x => x.Items)
            .WithOne()
            .HasForeignKey(x => x.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // GET /documents filters by user_id (+ optional type) and orders by created_at desc.
        builder.HasIndex(x => new { x.UserId, x.CreatedAt });
        // Backs GET /documents?customer_id=... — composite so one index covers the filter
        // (user_id + customer_id) AND the created_at sort in a single seek. No FK navigation
        // on purpose (avoids the soft-delete query-filter interaction; ownership/existence
        // is validated in the handler).
        builder.HasIndex(x => new { x.UserId, x.CustomerId, x.CreatedAt });
    }
}
