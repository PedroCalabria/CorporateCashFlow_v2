namespace CorporateTreasury.Application.Exceptions;

/// <summary>
/// Thrown when an authenticated Manager attempts an operation outside their scope — e.g. a
/// Subsidiary Manager acting on another subsidiary's user, creating a Manager, elevating a user to
/// global scope, or demoting/deactivating their own account. The Api maps it to <c>403 Forbidden</c>.
/// This is the fine-grained scope layer that sits on top of the coarse Manager policy (design.md §D2).
/// </summary>
public class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException(string message)
        : base(message)
    {
    }
}
