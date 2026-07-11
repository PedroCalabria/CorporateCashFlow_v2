namespace CorporateTreasury.Application.DTOs.Subsidiaries;

/// <summary>
/// Read model returned by the subsidiaries endpoints. Includes the bank account's baseline
/// (<see cref="InitialBalance"/> / <see cref="ReferenceDate"/>) for display only — these are
/// never mutable through any update path.
/// </summary>
public sealed record SubsidiaryResponse(
    Guid Id,
    string Name,
    string Code,
    bool IsActive,
    decimal InitialBalance,
    DateOnly ReferenceDate);
