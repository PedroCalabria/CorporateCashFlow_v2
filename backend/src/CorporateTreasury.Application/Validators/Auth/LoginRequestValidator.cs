using CorporateTreasury.Application.DTOs.Auth;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Auth;

/// <summary>
/// Validates <see cref="LoginRequest"/> shape only (email format, non-empty password).
/// Credential correctness is decided by <c>AuthService</c>, which returns a single generic
/// failure to avoid user enumeration — so validation deliberately does not probe existence.
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
