using FluentValidation;
using OfficeRkyv.Api.Shared.Validation;

namespace OfficeRkyv.Api.Features.Archives.Handlers;

/// <summary>
/// The input rules for a new archive name. They see the trimmed value (what the handler stores), and
/// each failure's error code is one of <see cref="ViolationCodes"/>. Public, and dependency-free, so
/// Wolverine's FluentValidation discovery finds it.
/// </summary>
public sealed class CreateArchiveValidator : AbstractValidator<CreateArchiveCommand>
{
    public const int MaxNameLength = 100;

    public CreateArchiveValidator() =>
        RuleFor(c => c.Name.Trim())
            .RequiredAndAtMost(MaxNameLength)
            .OverridePropertyName(nameof(CreateArchiveCommand.Name));
}

public sealed class RenameArchiveValidator : AbstractValidator<RenameArchiveCommand>
{
    public RenameArchiveValidator() =>
        RuleFor(c => c.Name.Trim())
            .RequiredAndAtMost(CreateArchiveValidator.MaxNameLength)
            .OverridePropertyName(nameof(RenameArchiveCommand.Name));
}
