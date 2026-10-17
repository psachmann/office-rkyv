using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using OfficeRkyv.Contracts;

namespace OfficeRkyv.UI.Auth;

/// <summary>
/// Makes the caller token (see <see cref="CallerTokens"/>) that tells the API which user a call is for: a JWT
/// signed with HMAC-SHA256, valid for <see cref="CallerTokens.Lifetime"/>. It is issued per call, because signing
/// is cheap and that spares any caching or refreshing.
/// </summary>
internal sealed class CallerTokenIssuer
{
    private readonly JsonWebTokenHandler handler = new();
    private readonly SigningCredentials credentials;
    private readonly TimeProvider time;

    /// <param name="key">At least <see cref="CallerTokens.MinimumKeyLength"/> bytes.</param>
    /// <param name="time">Tells the token when it was issued; tests move it to make an expired token.</param>
    public CallerTokenIssuer(byte[] key, TimeProvider? time = null)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length < CallerTokens.MinimumKeyLength)
        {
            throw new ArgumentException(
                $"The caller token signing key ({CallerTokens.SigningKeySetting}) must be at least {CallerTokens.MinimumKeyLength} bytes long.",
                nameof(key)
            );
        }

        credentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256);
        this.time = time ?? TimeProvider.System;
    }

    public string Issue(Guid userId)
    {
        var now = time.GetUtcNow().UtcDateTime;

        return handler.CreateToken(
            new SecurityTokenDescriptor
            {
                Issuer = CallerTokens.Issuer,
                Audience = CallerTokens.Audience,
                Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())]),
                IssuedAt = now,
                NotBefore = now,
                Expires = now + CallerTokens.Lifetime,
                SigningCredentials = credentials,
            }
        );
    }
}
