using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace InvoiceGen.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentSendTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastSendError",
                table: "documents",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastSendStatus",
                table: "documents",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "LastSentAt",
                table: "documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SendCount",
                table: "documents",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastSendError",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "LastSendStatus",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "LastSentAt",
                table: "documents");

            migrationBuilder.DropColumn(
                name: "SendCount",
                table: "documents");
        }
    }
}
