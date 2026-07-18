using Microsoft.IdentityModel.Tokens;
using RabiRiichi.Server.Utils;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace RabiRiichi.Server.Auth {
  public class TokenService {
    #region Static fields
    internal static readonly string Issuer = $"{nameof(RabiRiichi)}-{ServerConstants.SERVER}";
    internal static SymmetricSecurityKey SigningKey =
        new(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_SECRET")));
    internal static readonly TokenValidationParameters ValidationParameters = new() {
      ValidateIssuer = true,
      ValidateLifetime = true,
      ValidateIssuerSigningKey = true,
      ValidIssuer = Issuer,
      IssuerSigningKey = SigningKey,
      ValidateAudience = false,
    };
    internal static readonly SigningCredentials Credentials
        = new(SigningKey, SecurityAlgorithms.HmacSha256Signature);

    private const int TOKEN_DURATION_MINUTES = 60 * 24 * 7; // 7 days
    internal const string ServerInstanceClaim = "srv";
    #endregion

    private readonly JwtSecurityTokenHandler tokenHandler = new();
    // User ids are allocated from an in-memory list and can be reused after a
    // restart. Bind tokens to this process lifetime so an old id can never
    // authenticate as the new user that happens to receive the same id.
    private readonly string serverInstance =
        Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(8));

    public string BuildToken(int userId) {
      var claims = new[] {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString("x")),
        new Claim(ServerInstanceClaim, serverInstance),
      };

      var tokenDescriptor = new JwtSecurityToken(
          issuer: Issuer,
          claims: claims,
          expires: DateTime.Now.AddMinutes(TOKEN_DURATION_MINUTES),
          signingCredentials: Credentials);

      return tokenHandler.WriteToken(tokenDescriptor);
    }

    public bool IsTokenValid(string token, out int userId) {
      try {
        var claims = tokenHandler.ValidateToken(token, ValidationParameters, out _);
        if (!IsCurrentServerToken(claims)) {
          throw new SecurityTokenValidationException("Token belongs to a previous server instance");
        }
        userId = Convert.ToInt32(claims.FindFirst(ClaimTypes.NameIdentifier).Value, 16);
      } catch {
        userId = -1;
        return false;
      }
      return true;
    }

    public bool IsCurrentServerToken(ClaimsPrincipal principal) =>
        string.Equals(principal?.FindFirstValue(ServerInstanceClaim),
            serverInstance, StringComparison.Ordinal);
  }
}
