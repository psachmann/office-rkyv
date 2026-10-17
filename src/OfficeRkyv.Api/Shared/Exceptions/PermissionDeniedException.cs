namespace OfficeRkyv.Api.Shared.Exceptions;

/// <summary>
/// An accepted caller asked to change a user other than themselves. Reaches clients as
/// <c>PERMISSION_DENIED</c>.
/// </summary>
public sealed class PermissionDeniedException(string message) : Exception(message);
