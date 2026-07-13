namespace CorporateTreasury.Application.Services;

/// <summary>One structurally-parsed bank-statement row (semantic validation happens in the service).</summary>
public sealed record RawBankStatementRow(int RowNumber, string Date, string Amount, string Type, string Description, string? DocumentNumber);

/// <summary>Result of parsing an uploaded statement file: the well-formed rows plus per-row structural errors.</summary>
public sealed record BankStatementParseResult(
    IReadOnlyList<RawBankStatementRow> Rows,
    IReadOnlyList<(int RowNumber, string Message)> Errors);

/// <summary>
/// Minimal CSV parser for the bank-statement import (design.md §D4), mirroring
/// <see cref="LedgerEntryCsvParser"/>. Expected columns, with a header row:
/// <c>Date,Amount,Type,Description,DocumentNumber</c> — <c>DocumentNumber</c> optional. Description
/// must not contain a comma in the MVP CSV (fields are positional); structurally-malformed rows are
/// reported (never dropped) and business validation is applied later by the service.
/// </summary>
public static class BankStatementCsvParser
{
    private const int MinColumns = 4; // Date, Amount, Type, Description (DocumentNumber optional)

    public static BankStatementParseResult Parse(string content)
    {
        var rows = new List<RawBankStatementRow>();
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

            var parts = line.Split(',');
            if (parts.Length < MinColumns)
            {
                errors.Add((dataRowNumber, $"Expected at least {MinColumns} columns (Date, Amount, Type, Description[, DocumentNumber])."));
                continue;
            }

            rows.Add(new RawBankStatementRow(
                dataRowNumber,
                parts[0].Trim(),
                parts[1].Trim(),
                parts[2].Trim(),
                parts[3].Trim(),
                parts.Length >= 5 ? parts[4].Trim() : null));
        }

        return new BankStatementParseResult(rows, errors);
    }
}
