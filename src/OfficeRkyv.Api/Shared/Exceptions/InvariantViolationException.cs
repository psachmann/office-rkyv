namespace OfficeRkyv.Api.Shared.Exceptions;

public sealed class InvariantViolationException(string message) : Exception(message);
