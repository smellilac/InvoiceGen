using Microsoft.EntityFrameworkCore.Migrations;
using Pgvector;

#nullable disable

namespace InvoiceGen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmbeddingVectorColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "line_items",
                type: "vector(1024)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "documents",
                type: "vector(1024)",
                nullable: true);

            migrationBuilder.AddColumn<Vector>(
                name: "Embedding",
                table: "customers",
                type: "vector(1024)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_line_items_Embedding",
                table: "line_items",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_documents_Embedding",
                table: "documents",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_Embedding",
                table: "customers",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_line_items_Embedding",
                table: "line_items");

            migrationBuilder.DropIndex(
                name: "IX_documents_Embedding",
                table: "documents");

            migrationBuilder.DropIndex(
                name: "IX_customers_Embedding",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "line_items");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "Embedding",
                table: "customers");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:vector", ",,");
        }
    }
}
