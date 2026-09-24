namespace OfficeRkyv.Api.Shared.Exceptions;

/// <summary>A write targeted a resource that does not exist.</summary>
public sealed class NotFoundException(string message) : Exception(message);
