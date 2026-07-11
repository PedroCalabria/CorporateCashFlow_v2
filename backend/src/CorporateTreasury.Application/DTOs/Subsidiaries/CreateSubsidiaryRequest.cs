namespace CorporateTreasury.Application.DTOs.Subsidiaries;

/// <summary>
/// Body of <c>POST /api/subsidiaries</c>. Creates the subsidiary and its bank account together.
/// <see cref="InitialBalance"/> and <see cref="ReferenceDate"/> are accepted <b>only</b> here,
/// at creation — they are immutable afterwards and appear on no update contract (design.md §D4).
/// </summary>
public sealed record CreateSubsidiaryRequest(
    string Name,
    string Code,
    decimal InitialBalance,
    DateOnly ReferenceDate);
