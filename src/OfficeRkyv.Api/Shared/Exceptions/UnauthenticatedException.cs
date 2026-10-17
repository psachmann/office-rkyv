namespace OfficeRkyv.Api.Shared.Exceptions;

/// <summary>
/// A call needs a caller and has none the API accepts: no token, a token that does not verify or has expired,
/// or a token whose user does not exist or was deleted. Reaches clients as <c>UNAUTHENTICATED</c>.
/// </summary>
public sealed class UnauthenticatedException(string message) : Exception(message);
