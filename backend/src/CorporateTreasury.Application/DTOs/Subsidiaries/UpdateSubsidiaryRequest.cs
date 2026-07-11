namespace CorporateTreasury.Application.DTOs.Subsidiaries;

/// <summary>
/// Body of <c>PUT /api/subsidiaries/{id}</c>. Deliberately carries <b>only</b> the mutable
/// fields — there is no <c>InitialBalance</c> or <c>ReferenceDate</c> here, so a client cannot
/// even express a mutation of the immutable bank-account baseline (design.md §D4).
/// </summary>
public sealed record UpdateSubsidiaryRequest(
    string Name,
    string Code);
