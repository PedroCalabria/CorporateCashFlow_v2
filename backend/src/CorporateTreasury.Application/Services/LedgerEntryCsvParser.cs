namespace CorporateTreasury.Application.Services;

/// <summary>One structurally-parsed CSV row (semantic validation — category exists, amount &gt; 0 — happens in the service).</summary>
public sealed record RawLedgerImportRow(int RowNumber, string Date, string CategoryCode, string Amount, string Description);

/// <summary>Result of parsing an uploaded CSV: the well-formed rows plus per-row structural errors.</summary>
public sealed record LedgerImportParseResult(
    IReadOnlyList<RawLedgerImportRow> Rows,
    IReadOnlyList<(int RowNumber, string Message)> Errors);

/// <summary>
/// Minimal CSV parser for the ledger-entries bulk import (design.md §D4). Expected columns, with a
/// header row: <c>Date,CategoryCode,Amount,Description</c>. Structurally-malformed rows are reported
/// (never dropped); business validation is applied later by the service.
/// </summary>
public static class LedgerEntryCsvParser
{
    private const int ExpectedColumns = 4;

    public static LedgerImportParseResult Parse(string content)
    {
        var rows = new List<RawLedgerImportRow>();
        var errors = new List<(int, string)>();

        // Split on any line ending; ignore blank lines. Row numbers are 1-based over data rows
        // (the header is row 0 and skipped) so they match what a user sees in a spreadsheet.
        var lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var dataRowNumber = 0;
        var headerSkipped = false;

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (!headerSkipped)
            {
                headerSkipped = true;
                continue; // first non-blank line is the header
            }

            dataRowNumber++;

            // Description is the last field and may itself contain commas.
            var parts = line.Split(',', ExpectedColumns);
            if (parts.Length < ExpectedColumns)
            {
                errors.Add((dataRowNumber, $"Expected {ExpectedColumns} columns (Date, CategoryCode, Amount, Description)."));
                continue;
            }

            rows.Add(new RawLedgerImportRow(
                dataRowNumber,
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                parts[3].Trim()));
        }

        return new LedgerImportParseResult(rows, errors);
    }
}
