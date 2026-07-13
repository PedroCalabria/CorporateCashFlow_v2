using System.Globalization;
using ClosedXML.Excel;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Parses the bank-statement import from an <c>.xlsx</c> workbook (design.md §D4), mirroring
/// <see cref="LedgerEntryXlsxParser"/>. Expected columns A–E, with a header row:
/// <c>Date | Amount | Type | Description | DocumentNumber</c> (<c>DocumentNumber</c> optional).
/// Produces the same <see cref="BankStatementParseResult"/> as <see cref="BankStatementCsvParser"/>
/// so the semantic validation in <see cref="BankStatementImportService.ImportAsync"/> is reused unchanged.
/// </summary>
/// <remarks>
/// Cells are normalized to the canonical string shape the CSV path validates: a real Excel date cell
/// becomes <c>yyyy-MM-dd</c> and a numeric cell becomes an invariant-culture number (dot decimal, no
/// thousands separator), so parsing is independent of the author's machine locale.
/// </remarks>
public static class BankStatementXlsxParser
{
    public static BankStatementParseResult Parse(Stream content)
    {
        var rows = new List<RawBankStatementRow>();
        var errors = new List<(int RowNumber, string Message)>();

        XLWorkbook workbook;
        try
        {
            workbook = new XLWorkbook(content);
        }
        catch (Exception)
        {
            // Corrupt / not a real .xlsx: report as a single structural error (never throw at the user).
            errors.Add((0, "Invalid or unreadable .xlsx file."));
            return new BankStatementParseResult(rows, errors);
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                return new BankStatementParseResult(rows, errors);
            }

            // Row numbers are 1-based over data rows (header skipped) so they match what a user sees.
            var dataRowNumber = 0;
            var headerSkipped = false;

            foreach (var row in worksheet.RowsUsed())
            {
                if (!headerSkipped)
                {
                    headerSkipped = true;
                    continue; // first non-empty row is the header
                }

                dataRowNumber++;

                var documentNumber = row.Cell(5).GetString().Trim();
                rows.Add(new RawBankStatementRow(
                    dataRowNumber,
                    DateCellToString(row.Cell(1)),
                    NumberCellToString(row.Cell(2)),
                    row.Cell(3).GetString().Trim(),
                    row.Cell(4).GetString().Trim(),
                    string.IsNullOrWhiteSpace(documentNumber) ? null : documentNumber));
            }
        }

        return new BankStatementParseResult(rows, errors);
    }

    // A real date cell → canonical yyyy-MM-dd; anything else (text) passes through for the service to validate.
    private static string DateCellToString(IXLCell cell) =>
        cell.Value.IsDateTime
            ? cell.Value.GetDateTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : cell.GetString().Trim();

    // A numeric cell → invariant-culture number (dot decimal, no grouping); text passes through unchanged.
    private static string NumberCellToString(IXLCell cell) =>
        cell.Value.IsNumber
            ? cell.Value.GetNumber().ToString(CultureInfo.InvariantCulture)
            : cell.GetString().Trim();
}
