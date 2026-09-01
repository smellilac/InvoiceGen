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
    }
}
