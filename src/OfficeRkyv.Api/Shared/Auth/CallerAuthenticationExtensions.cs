using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace OfficeRkyv.Api.Shared.Auth;

public static class CallerAuthenticationExtensions
{
    /// <summary>
    /// Validates the caller token every call carries (see <see cref="CallerTokens"/>): signature, issuer,
    /// audience and lifetime. A token that fails any of it leaves the call without a principal, which
    /// <see cref="CallerAccessor"/> refuses as unauthenticated - through the one error mapping, and not through
    /// the authorization middleware's own HTTP 401.
    /// </summary>
    /// <param name="services">The host's services.</param>
    /// <param name="key">The HMAC key the UI host signs with; at least <see cref="CallerTokens.MinimumKeyLength"/> bytes.</param>
    public static IServiceCollection AddCallerAuthentication(this IServiceCollection services, byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (key.Length < CallerTokens.MinimumKeyLength)
        {
            throw new ArgumentException(
                $"The caller token signing key ({CallerTokens.SigningKeySetting}) must be at least {CallerTokens.MinimumKeyLength} bytes long.",
                nameof(key)
            );
        }

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keeps the claim named `sub`, as the token has it, instead of renaming it to a .NET claim type.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    RequireSignedTokens = true,
                    ValidateIssuer = true,
                    ValidIssuer = CallerTokens.Issuer,
                    ValidateAudience = true,
                    ValidAudience = CallerTokens.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                };
            });

        services.AddScoped<CallerAccessor>();

        return services;
    }
}
