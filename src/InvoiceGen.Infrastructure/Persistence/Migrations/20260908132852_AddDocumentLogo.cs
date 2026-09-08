using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceGen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentLogo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LogoUrl",
                table: "documents",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LogoUrl",
                table: "documents");
        }
    }
}
