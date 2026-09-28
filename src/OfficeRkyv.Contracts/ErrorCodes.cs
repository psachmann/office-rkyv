namespace OfficeRkyv.Contracts;

/// <summary>
/// The <c>reason</c> of a <c>google.rpc.BadRequest</c> field violation: which input rule a field broke.
/// Stable wire values - clients key their own localized text off them, so never reword one.
/// </summary>
public static class ViolationCodes
{
    public const string Required = "required";
    public const string InvalidFormat = "invalid_format";
    public const string TooLong = "too_long";
}

/// <summary>
/// The <c>reason</c> of the <c>google.rpc.ErrorInfo</c> sent with <c>FAILED_PRECONDITION</c>: which
/// business rule refused an otherwise well-formed request. Stable wire values, like <see cref="ViolationCodes"/>.
/// </summary>
public static class InvariantCodes
{
    public const string UserDeleted = "user_deleted";
}

/// <summary>The <c>domain</c> of every <c>google.rpc.ErrorInfo</c> this API sends.</summary>
public static class ErrorDomain
{
    public const string Value = "office-rkyv";
}
