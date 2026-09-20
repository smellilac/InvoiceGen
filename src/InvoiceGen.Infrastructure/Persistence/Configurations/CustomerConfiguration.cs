using InvoiceGen.Application.Features.Customers;
using InvoiceGen.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace InvoiceGen.Infrastructure.Persistence.Configurations;

public sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name).IsRequired().HasMaxLength(CustomerLimits.NameMax);
        builder.Property(x => x.Email).HasMaxLength(CustomerLimits.EmailMax);
        builder.Property(x => x.Address).HasMaxLength(CustomerLimits.AddressMax);
        builder.Property(x => x.Phone).HasMaxLength(CustomerLimits.PhoneMax);
        builder.Property(x => x.Notes).HasMaxLength(CustomerLimits.NotesMax);

        builder.HasOne<AppUser>()
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        // Soft delete: a global filter hides deleted rows from every query automatically,
        // so listing, get, and customer_id validation all exclude them without extra code.
        builder.HasQueryFilter(c => c.DeletedAt == null);

        builder.Ignore(x => x.IsDeleted);

        // GET /customers filters by user_id and orders by name.
        builder.HasIndex(x => new { x.UserId, x.Name });

        // pgvector embedding column (1536 dims, OpenAI text-embedding-3-small size).
        builder.Property(x => x.Embedding).HasColumnType("vector(1536)");
        // HNSW index for approximate nearest-neighbour search under cosine distance.
        builder.HasIndex(x => x.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
