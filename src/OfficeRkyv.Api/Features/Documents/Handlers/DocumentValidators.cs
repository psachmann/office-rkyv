using FluentValidation;
using OfficeRkyv.Api.Shared.Validation;

namespace OfficeRkyv.Api.Features.Documents.Handlers;

/// <summary>
/// The input rules for a document title. They see the trimmed value (what the handler stores), and
/// each failure's error code is one of <see cref="ViolationCodes"/>. Public, and dependency-free, so
/// Wolverine's FluentValidation discovery finds it.
/// </summary>
public sealed class CreateDocumentValidator : AbstractValidator<CreateDocumentCommand>
{
    public const int MaxTitleLength = 200;

    public CreateDocumentValidator() =>
        RuleFor(c => c.Title.Trim())
            .RequiredAndAtMost(MaxTitleLength)
            .OverridePropertyName(nameof(CreateDocumentCommand.Title));
}

public sealed class RenameDocumentValidator : AbstractValidator<RenameDocumentCommand>
{
    public RenameDocumentValidator() =>
        RuleFor(c => c.Title.Trim())
            .RequiredAndAtMost(CreateDocumentValidator.MaxTitleLength)
            .OverridePropertyName(nameof(RenameDocumentCommand.Title));
}
