namespace CorporateTreasury.Domain.Common;

/// <summary>
/// The well-known actor id used for system-triggered domain changes that have no human performer —
/// principally the automatic reconciliation match pass (§1.2 transition 3, <c>PerformedBy = System</c>).
/// A fixed, reserved <see cref="Guid"/> so audit rows written by the engine are attributable and
/// distinguishable from any real user.
/// </summary>
public static class SystemActor
{
    /// <summary>Reserved id for the system/automatic actor.</summary>
    public static readonly Guid Id = new("00000000-0000-0000-0000-000000000005");
}
