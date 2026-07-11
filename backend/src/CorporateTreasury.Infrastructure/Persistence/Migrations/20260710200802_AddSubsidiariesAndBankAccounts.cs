using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorporateTreasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubsidiariesAndBankAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Subsidiaries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subsidiaries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubsidiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    InitialBalance = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ReferenceDate = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankAccounts_Subsidiaries_SubsidiaryId",
                        column: x => x.SubsidiaryId,
                        principalTable: "Subsidiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_SubsidiaryId",
                table: "Users",
                column: "SubsidiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_SubsidiaryId",
                table: "BankAccounts",
                column: "SubsidiaryId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Subsidiaries_Code",
                table: "Subsidiaries",
                column: "Code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Users_Subsidiaries_SubsidiaryId",
                table: "Users",
                column: "SubsidiaryId",
                principalTable: "Subsidiaries",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Users_Subsidiaries_SubsidiaryId",
                table: "Users");

            migrationBuilder.DropTable(
                name: "BankAccounts");

            migrationBuilder.DropTable(
                name: "Subsidiaries");

            migrationBuilder.DropIndex(
                name: "IX_Users_SubsidiaryId",
                table: "Users");
        }
    }
}
