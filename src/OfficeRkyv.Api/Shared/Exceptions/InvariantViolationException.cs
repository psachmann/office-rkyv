namespace OfficeRkyv.Api.Shared.Exceptions;

/// <summary>
/// A well-formed request the resource's current state refuses. <see cref="Code"/> is one of
/// <see cref="InvariantCodes"/> and reaches clients as a stable reason they localize themselves.
/// </summary>
public sealed class InvariantViolationException(string code) : Exception($"Invariant violated: {code}.")
{
    public string Code { get; } = code;
}
