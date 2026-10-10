using FluentValidation;
using OfficeRkyv.Api.Shared.Validation;

namespace OfficeRkyv.Api.Features.Documents.Handlers;

/// <summary>
/// The input rules for a new document: its title, and the name and type of its file. They see the trimmed
/// value (what the handler stores), and each failure's error code is one of <see cref="ViolationCodes"/>.
/// Public, and dependency-free, so Wolverine's FluentValidation discovery finds it. The content itself is
/// refused earlier, while it is read (see <see cref="DocumentIngest"/>), because there is no command yet.
/// </summary>
public sealed class CreateDocumentValidator : AbstractValidator<CreateDocumentCommand>
{
    public const int MaxTitleLength = 200;
    public const int MaxFileNameLength = 255;
    public const int MaxContentTypeLength = 255;

    public CreateDocumentValidator()
    {
        RuleFor(c => c.Title.Trim())
            .RequiredAndAtMost(MaxTitleLength)
            .OverridePropertyName(nameof(CreateDocumentCommand.Title));

        RuleFor(c => c.FileName.Trim())
            .RequiredAndAtMost(MaxFileNameLength)
            .OverridePropertyName(nameof(CreateDocumentCommand.FileName));

        // An absent content type was defaulted before the command was made, so only the length is checked.
        RuleFor(c => c.ContentType.Trim())
            .MaximumLength(MaxContentTypeLength)
            .WithErrorCode(ViolationCodes.TooLong)
            .OverridePropertyName(nameof(CreateDocumentCommand.ContentType));
    }
}

public sealed class RenameDocumentValidator : AbstractValidator<RenameDocumentCommand>
{
    public RenameDocumentValidator() =>
        RuleFor(c => c.Title.Trim())
            .RequiredAndAtMost(CreateDocumentValidator.MaxTitleLength)
            .OverridePropertyName(nameof(RenameDocumentCommand.Title));
}
