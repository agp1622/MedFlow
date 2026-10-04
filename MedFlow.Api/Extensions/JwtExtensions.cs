using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MedFlow.Infrastructure.Identity;
using Microsoft.IdentityModel.Tokens;

namespace MedFlow.Api.Extensions;

public static class JwtExtensions
{
    public static string GenerateToken(this ApplicationUser user, IConfiguration config, IEnumerable<string> roles)
    {
        var jwtSettings = config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddMinutes(int.Parse(jwtSettings["ExpiryMinutes"] ?? "60"));

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Email, user.Email ?? ""),
            new Claim(ClaimTypes.GivenName, user.FirstName),
            new Claim(ClaimTypes.Surname, user.LastName),
            new Claim("specialty", user.Specialty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(r => new Claim(ClaimTypes.Role, r)));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expiry,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private const string ChallengePurpose = "2fa-challenge";

    /// <summary>Audience of 2FA challenges. It differs from the access-token audience, so the bearer middleware rejects a challenge.</summary>
    private static string ChallengeAudience(IConfiguration config) => config["Jwt:Audience"] + ":2fa";

    /// <summary>
    /// Short-lived, single-purpose token handed out after the first factor. Carries only the user id: no roles, so it grants nothing.
    /// </summary>
    public static (string Token, DateTime Expires) GenerateTwoFactorChallenge(this ApplicationUser user, IConfiguration config)
    {
        var jwt = config.GetSection("Jwt");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!));
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(jwt["TwoFactorChallengeMinutes"] ?? "5"));
        var token = new JwtSecurityToken(
            issuer: jwt["Issuer"],
            audience: ChallengeAudience(config),
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim("purpose", ChallengePurpose),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            expires: expires,
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }

    /// <summary>The user id inside a valid challenge, or null for anything else (expired, tampered, an access token...).</summary>
    public static string? ValidateTwoFactorChallenge(string? token, IConfiguration config)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        var jwt = config.GetSection("Jwt");
        try
        {
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = jwt["Issuer"],
                ValidAudience = ChallengeAudience(config),
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["Key"]!)),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
                ClockSkew = TimeSpan.Zero
            }, out _);
            if (principal.FindFirstValue("purpose") != ChallengePurpose) return null;
            return principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        }
        catch (Exception)
        {
            return null;
        }
    }

    public static string GetUserId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User not authenticated.");
}
