namespace OfficeRkyv.Api.Shared;

public sealed class InvariantViolationException(string message) : Exception(message);
