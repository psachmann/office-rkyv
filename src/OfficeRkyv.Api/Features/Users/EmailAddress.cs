namespace OfficeRkyv.Api.Features.Users;

public static class EmailAddress
{
    public const int MaxLength = 254;

    /// <summary>
    /// The uniqueness key for an email: trimmed and lowercased with culture-invariant rules. Applied
    /// once, at write time, and recorded in <see cref="UserCreated"/> - never recomputed on replay, so
    /// changing this rule later cannot silently re-key existing users.
    /// </summary>
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();

    /// <summary>
    /// A deliberately small plausibility check on an already-trimmed email: exactly one '@', something
    /// before it, a domain after it with a '.' that is neither its first nor its last character, and no
    /// whitespace. It rejects typos, not every address a mail server would refuse.
    /// </summary>
    public static bool IsWellFormed(string email)
    {
        if (email.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var at = email.IndexOf('@');

        if (at <= 0 || at != email.LastIndexOf('@'))
        {
            return false;
        }

        var domain = email.AsSpan(at + 1);

        return domain.Contains('.') && domain[0] != '.' && domain[^1] != '.';
    }
}
