namespace OfficeRkyv.Api.Shared.Exceptions;

/// <summary>
/// One violated rule. <see cref="Field"/> is the command property it concerns; <see cref="Code"/> is a
/// stable machine-readable reason (see <see cref="ViolationCodes"/>) so clients can choose their own
/// localized text without parsing English.
/// </summary>
public sealed record FieldViolation(string Field, string Code);

public static class ViolationCodes
{
    public const string Required = "required";
    public const string InvalidFormat = "invalid_format";
    public const string TooLong = "too_long";
}

/// <summary>
/// A command's input broke one or more validation rules. Carries every violation, not just the first,
/// so a form can flag all offending fields in one round trip.
/// </summary>
public sealed class ValidationException(IReadOnlyList<FieldViolation> violations)
    : Exception($"Invalid input: {string.Join(", ", violations.Select(v => $"{v.Field} ({v.Code})"))}.")
{
    public IReadOnlyList<FieldViolation> Violations { get; } = violations;
}
