using InvoiceGen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceGen.Infrastructure.Persistence.Configurations;

public sealed class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        // ISO 4217 codes are 3 chars, but allow headroom for crypto tickers
        // (USDT, USDC) or non-standard identifiers.
        builder.Property(x => x.DefaultCurrency).HasMaxLength(10);

        builder.Property(x => x.BusinessName).HasMaxLength(200);
        builder.Property(x => x.LogoUrl).HasMaxLength(2048);
        // BusinessAddress is intentionally left unbounded (multiline free text).

        // Google `sub` for Google-linked accounts. Unique so one Google identity maps to at
        // most one local account; the filtered index lets the many password-only accounts
        // (GoogleId == null) coexist without colliding on a single NULL.
        builder.Property(x => x.GoogleId).HasMaxLength(255);
        builder.HasIndex(x => x.GoogleId)
            .IsUnique()
            .HasFilter("\"GoogleId\" IS NOT NULL");
    }
}
