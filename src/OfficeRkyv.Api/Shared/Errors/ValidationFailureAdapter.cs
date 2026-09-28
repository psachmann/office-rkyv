using FluentValidation;
using Google.Rpc;
using Wolverine.Grpc;

namespace OfficeRkyv.Api.Shared.Errors;

/// <summary>
/// Turns the validation middleware's failure into <c>google.rpc.BadRequest</c> field violations. Used
/// instead of Wolverine's packaged FluentValidation adapter, which leaves <c>reason</c> empty and puts
/// English text where clients would have to parse it: here the error code travels as <c>reason</c>.
/// </summary>
internal sealed class ValidationFailureAdapter : IValidationFailureAdapter
{
    public bool CanHandle(Exception exception) => exception is ValidationException;

    public IEnumerable<BadRequest.Types.FieldViolation> ToFieldViolations(Exception exception) =>
        ((ValidationException)exception).Errors.Select(failure => new BadRequest.Types.FieldViolation
        {
            // Validators report the command's property names; the contract's field names are snake_case.
            Field = ToSnakeCase(failure.PropertyName),
            Reason = failure.ErrorCode,
            // For developers only - clients localize from Reason and never show this.
            Description = failure.ErrorMessage,
        });

    internal static string ToSnakeCase(string name) =>
        string.Concat(
            name.Select(
                (c, i) => i > 0 && char.IsUpper(c) ? $"_{char.ToLowerInvariant(c)}" : $"{char.ToLowerInvariant(c)}"
            )
        );
}
