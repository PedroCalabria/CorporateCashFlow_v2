using CorporateTreasury.Domain.Enums;

namespace CorporateTreasury.Domain.Entities;

/// <summary>
/// A fixed catalog category (docs/requirements-document.md §3.5), bound to a
/// <see cref="CategoryType"/> so reports can aggregate income vs. expense automatically. Reference
/// data: seeded in every environment and never edited through the app. <see cref="Code"/> is a
/// stable identifier used by the spreadsheet import.
/// </summary>
public class Category
{
    private Category()
    {
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    /// <summary>Stable machine code (e.g. <c>SALES_REVENUE</c>), unique across the catalog.</summary>
    public string Code { get; private set; } = null!;

    public CategoryType Type { get; private set; }

    public static Category Create(Guid id, string name, string code, CategoryType type) =>
        new()
        {
            Id = id,
            Name = name,
            Code = code,
            Type = type,
        };
}
