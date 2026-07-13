using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CorporateTreasury.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddBankStatementImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BankStatementImportBatches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SubsidiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FileName = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    RejectedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementImportBatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementImportBatches_Subsidiaries_SubsidiaryId",
                        column: x => x.SubsidiaryId,
                        principalTable: "Subsidiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BankStatementLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportBatchId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubsidiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DocumentNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MatchedLedgerEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankStatementLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_BankStatementImportBatches_ImportBatchId",
                        column: x => x.ImportBatchId,
                        principalTable: "BankStatementImportBatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_LedgerEntries_MatchedLedgerEntryId",
                        column: x => x.MatchedLedgerEntryId,
                        principalTable: "LedgerEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BankStatementLines_Subsidiaries_SubsidiaryId",
                        column: x => x.SubsidiaryId,
                        principalTable: "Subsidiaries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImportBatches_Status",
                table: "BankStatementImportBatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementImportBatches_SubsidiaryId",
                table: "BankStatementImportBatches",
                column: "SubsidiaryId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_ImportBatchId",
                table: "BankStatementLines",
                column: "ImportBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_MatchedLedgerEntryId",
                table: "BankStatementLines",
                column: "MatchedLedgerEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_Status",
                table: "BankStatementLines",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_BankStatementLines_SubsidiaryId",
                table: "BankStatementLines",
                column: "SubsidiaryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankStatementLines");

            migrationBuilder.DropTable(
                name: "BankStatementImportBatches");
        }
    }
}
