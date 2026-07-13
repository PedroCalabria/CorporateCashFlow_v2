using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CorporateTreasury.Infrastructure.Persistence;

/// <summary>
/// Seeds the fixed <see cref="Category"/> catalog (docs/requirements-document.md §3.5). Unlike the
/// development-only user seed, categories are reference data the app needs to function, so this runs
/// in <b>every</b> environment. Idempotent: inserts only the catalog entries whose <c>Code</c> is
/// absent, so it is safe to run on every startup.
/// </summary>
public static class CategoryReferenceSeeder
{
    private static readonly (string Name, string Code, CategoryType Type)[] Catalog =
    [
        ("Sales Revenue", "SALES_REVENUE", CategoryType.Income),
        ("Other Revenue", "OTHER_REVENUE", CategoryType.Income),
        ("Balance Correction (Increase)", "BALANCE_CORRECTION_INCREASE", CategoryType.Income),
        ("Suppliers", "SUPPLIERS", CategoryType.Expense),
        ("Payroll", "PAYROLL", CategoryType.Expense),
        ("Taxes", "TAXES", CategoryType.Expense),
        ("Administrative Expenses", "ADMINISTRATIVE_EXPENSES", CategoryType.Expense),
        ("Financial Expenses", "FINANCIAL_EXPENSES", CategoryType.Expense),
        ("Investments", "INVESTMENTS", CategoryType.Expense),
        ("Loans/Financing", "LOANS_FINANCING", CategoryType.Expense),
        ("Other Expenses", "OTHER_EXPENSES", CategoryType.Expense),
        ("Balance Correction (Decrease)", "BALANCE_CORRECTION_DECREASE", CategoryType.Expense),
    ];

    public static async Task SeedAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        var existingCodes = (await db.Categories.Select(c => c.Code).ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var toAdd = Catalog
            .Where(c => !existingCodes.Contains(c.Code))
            .Select(c => Category.Create(Guid.NewGuid(), c.Name, c.Code, c.Type))
            .ToList();

        if (toAdd.Count > 0)
        {
            db.Categories.AddRange(toAdd);
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
