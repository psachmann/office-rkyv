using FluentValidation;

namespace OfficeRkyv.Api.Features.Users.Handlers;

/// <summary>
/// The input rules for <see cref="CreateUserCommand"/>. Every rule sees the trimmed value (what
/// <see cref="CreateUserHandler.Handle"/> actually stores), and each failure's error code is one of
/// <see cref="ViolationCodes"/> so it reaches clients as a stable reason rather than English text.
/// Public so Wolverine's FluentValidation discovery finds it; it takes no dependencies, which keeps the
/// generated middleware free of service location.
/// </summary>
public sealed class CreateUserCommandValidator : AbstractValidator<CreateUserCommand>
{
    public CreateUserCommandValidator()
    {
        // The rules run on the trimmed value, so the property name is pinned back to the command
        // property: the expression's own name would be derived from the Trim() call.
        RuleFor(c => c.Email.Trim())
            .NotEmpty()
            .WithErrorCode(ViolationCodes.Required)
            .OverridePropertyName(nameof(CreateUserCommand.Email));

        // Length and shape are only worth reporting for an email that is present; an empty one is
        // already "required" and would otherwise also fail as malformed.
        RuleFor(c => c.Email.Trim())
            .MaximumLength(EmailAddress.MaxLength)
            .WithErrorCode(ViolationCodes.TooLong)
            .Must(EmailAddress.IsWellFormed)
            .WithErrorCode(ViolationCodes.InvalidFormat)
            .OverridePropertyName(nameof(CreateUserCommand.Email))
            .When(c => c.Email.Trim().Length > 0);

        RuleFor(c => c.DisplayName.Trim())
            .NotEmpty()
            .WithErrorCode(ViolationCodes.Required)
            .MaximumLength(CreateUserHandler.MaxDisplayNameLength)
            .WithErrorCode(ViolationCodes.TooLong)
            .OverridePropertyName(nameof(CreateUserCommand.DisplayName));
    }
}
