using System.Globalization;
using ClosedXML.Excel;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Parses the ledger-entries bulk import from an <c>.xlsx</c> workbook (design.md §D4). Expected
/// columns A–D, with a header row: <c>Date | CategoryCode | Amount | Description</c>. Produces the same
/// <see cref="LedgerImportParseResult"/> as <see cref="LedgerEntryCsvParser"/> so the semantic
/// validation in <see cref="LedgerEntryService.ImportAsync"/> is reused unchanged.
/// </summary>
/// <remarks>
/// Cells are normalized to the canonical string shape the CSV path already validates: a real Excel
/// date cell becomes <c>yyyy-MM-dd</c> and a numeric cell becomes an invariant-culture number (dot
/// decimal, no thousands separator), so parsing is independent of the author's machine locale.
/// Columns are positional in a spreadsheet, so a blank Description is left blank and reported by the
/// service ("Description is required") rather than as a structural column-count error.
/// </remarks>
public static class LedgerEntryXlsxParser
{
    public static LedgerImportParseResult Parse(Stream content)
    {
        var rows = new List<RawLedgerImportRow>();
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
            return new LedgerImportParseResult(rows, errors);
        }

        using (workbook)
        {
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet is null)
            {
                return new LedgerImportParseResult(rows, errors);
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

                rows.Add(new RawLedgerImportRow(
                    dataRowNumber,
                    DateCellToString(row.Cell(1)),
                    row.Cell(2).GetString().Trim(),
                    NumberCellToString(row.Cell(3)),
                    row.Cell(4).GetString().Trim()));
            }
        }

        return new LedgerImportParseResult(rows, errors);
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
