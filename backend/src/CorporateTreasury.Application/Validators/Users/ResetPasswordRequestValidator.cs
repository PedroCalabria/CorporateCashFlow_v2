using CorporateTreasury.Application.DTOs.Users;
using FluentValidation;

namespace CorporateTreasury.Application.Validators.Users;

/// <summary>
/// Validates <see cref="ResetPasswordRequest"/>: a minimally strong new password (length plus at
/// least one letter and one digit). No email flow is involved — the Manager sets it directly.
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Za-z]").WithMessage("The password must contain at least one letter.")
            .Matches("[0-9]").WithMessage("The password must contain at least one digit.");
    }
}
