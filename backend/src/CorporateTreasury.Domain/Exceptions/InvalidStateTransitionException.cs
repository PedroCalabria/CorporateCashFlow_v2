namespace CorporateTreasury.Domain.Exceptions;

/// <summary>
/// Thrown when a domain entity is asked to make a state transition its guards forbid
/// (e.g. deactivating a <c>Subsidiary</c> that still has active users). Carries an optional
/// machine-readable <see cref="Code"/> so the Api can map it to a stable, translatable
/// client error without string-matching the message.
/// </summary>
public class InvalidStateTransitionException : Exception
{
    public InvalidStateTransitionException(string message, string? code = null)
        : base(message)
    {
        Code = code;
    }

    /// <summary>Stable, machine-readable identifier for this failure (e.g. for i18n keying).</summary>
    public string? Code { get; }
}
