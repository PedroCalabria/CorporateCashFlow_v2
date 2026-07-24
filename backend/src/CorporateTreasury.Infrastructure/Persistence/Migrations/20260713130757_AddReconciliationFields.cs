using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorporateTreasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReconciliationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "LedgerEntries",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "LedgerEntries");
        }
    }
}
