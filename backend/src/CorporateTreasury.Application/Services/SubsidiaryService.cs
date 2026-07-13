using CorporateTreasury.Application.DTOs.Subsidiaries;
using CorporateTreasury.Domain.Entities;
using CorporateTreasury.Domain.Interfaces;

namespace CorporateTreasury.Application.Services;

/// <summary>
/// Orchestrates subsidiary management over <see cref="ISubsidiaryRepository"/>. Called directly
/// by the Api controller (no MediatR — design.md §D1). RBAC (Global-Manager-only) is enforced at
/// the Api boundary; this service assumes an authorized caller and owns the business orchestration:
/// atomic create, edits, and the guarded deactivate/reactivate transitions.
/// </summary>
public sealed class SubsidiaryService
{
    private readonly ISubsidiaryRepository _subsidiaries;
    private readonly ILedgerEntryRepository _ledgerEntries;

    public SubsidiaryService(ISubsidiaryRepository subsidiaries, ILedgerEntryRepository ledgerEntries)
    {
        _subsidiaries = subsidiaries;
        _ledgerEntries = ledgerEntries;
    }

    /// <summary>Creates the subsidiary and its bank account together (one transaction).</summary>
    public async Task<SubsidiaryResponse> CreateAsync(CreateSubsidiaryRequest request, CancellationToken cancellationToken = default)
    {
        var subsidiary = Subsidiary.Create(
            request.Name,
            request.Code,
            request.InitialBalance,
            request.ReferenceDate);

        await _subsidiaries.AddAsync(subsidiary, cancellationToken);
        await _subsidiaries.SaveChangesAsync(cancellationToken);

        return ToResponse(subsidiary);
    }

    /// <summary>All subsidiaries, active and inactive.</summary>
    public async Task<IReadOnlyList<SubsidiaryResponse>> ListAsync(CancellationToken cancellationToken = default)
    {
        var subsidiaries = await _subsidiaries.ListAllAsync(cancellationToken);
        return subsidiaries.Select(ToResponse).ToList();
    }

    /// <summary>Edits Name/Code only. Returns <c>null</c> if the subsidiary does not exist.</summary>
    public async Task<SubsidiaryResponse?> UpdateAsync(Guid id, UpdateSubsidiaryRequest request, CancellationToken cancellationToken = default)
    {
        var subsidiary = await _subsidiaries.GetByIdAsync(id, cancellationToken);
        if (subsidiary is null)
        {
            return null;
        }

        subsidiary.UpdateDetails(request.Name, request.Code);
        await _subsidiaries.SaveChangesAsync(cancellationToken);

        return ToResponse(subsidiary);
    }

    /// <summary>
    /// Deactivates a subsidiary. Returns <c>null</c> if it does not exist. Throws
    /// <see cref="Domain.Exceptions.InvalidStateTransitionException"/> when the guard blocks it —
    /// the domain enforces the full §4 rule; this method supplies both inputs: the active-user
    /// count and whether any non-terminal ledger entry exists.
    /// </summary>
    public async Task<SubsidiaryResponse?> DeactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subsidiary = await _subsidiaries.GetByIdAsync(id, cancellationToken);
        if (subsidiary is null)
        {
            return null;
        }

        var activeUserCount = await _subsidiaries.CountActiveUsersAsync(id, cancellationToken);
        var hasNonTerminalEntries = await _ledgerEntries.HasNonTerminalEntriesAsync(id, cancellationToken);
        subsidiary.Deactivate(activeUserCount, hasNonTerminalEntries);
        await _subsidiaries.SaveChangesAsync(cancellationToken);

        return ToResponse(subsidiary);
    }

    /// <summary>Reactivates a subsidiary. Returns <c>null</c> if it does not exist.</summary>
    public async Task<SubsidiaryResponse?> ReactivateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var subsidiary = await _subsidiaries.GetByIdAsync(id, cancellationToken);
        if (subsidiary is null)
        {
            return null;
        }

        subsidiary.Reactivate();
        await _subsidiaries.SaveChangesAsync(cancellationToken);

        return ToResponse(subsidiary);
    }

    private static SubsidiaryResponse ToResponse(Subsidiary s) => new(
        s.Id,
        s.Name,
        s.Code,
        s.IsActive,
        s.BankAccount.InitialBalance,
        s.BankAccount.ReferenceDate);
}
