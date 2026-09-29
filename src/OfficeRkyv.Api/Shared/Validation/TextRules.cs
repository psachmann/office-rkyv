using FluentValidation;

namespace OfficeRkyv.Api.Shared.Validation;

internal static class TextRules
{
    /// <summary>
    /// The rules for a free-text field that is stored trimmed: required, and no longer than
    /// <paramref name="maxLength"/>. Apply it to the already-trimmed value, and pin the property name
    /// back to the command property, because the expression's own name would come from the Trim() call.
    /// </summary>
    public static IRuleBuilderOptions<T, string> RequiredAndAtMost<T>(
        this IRuleBuilder<T, string> rule,
        int maxLength
    ) =>
        rule.NotEmpty()
            .WithErrorCode(ViolationCodes.Required)
            .MaximumLength(maxLength)
            .WithErrorCode(ViolationCodes.TooLong);
}
