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
    #endregion

    internal const string TokenVersionClaim = "tv";

    private readonly JwtSecurityTokenHandler tokenHandler = new();

    public string BuildToken(int userId, int tokenVersion = 0) {
      var claims = new[] {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString("x")),
        new Claim(TokenVersionClaim, tokenVersion.ToString()),
      };

      var tokenDescriptor = new JwtSecurityToken(
          issuer: Issuer,
          claims: claims,
          expires: DateTime.Now.AddMinutes(TOKEN_DURATION_MINUTES),
          signingCredentials: Credentials);

      return tokenHandler.WriteToken(tokenDescriptor);
    }

    /// <summary>
    /// Validates the token signature and lifetime only (no DB access), so it is
    /// safe on the hot path. The token version is surfaced for callers that want
    /// to additionally check it against the DB (done once per WS connection).
    /// </summary>
    public bool IsTokenValid(string token, out int userId, out int tokenVersion) {
      tokenVersion = 0;
      try {
        var claims = tokenHandler.ValidateToken(token, ValidationParameters, out _);
        userId = Convert.ToInt32(claims.FindFirst(ClaimTypes.NameIdentifier).Value, 16);
        var tv = claims.FindFirst(TokenVersionClaim)?.Value;
        if (tv != null) {
          tokenVersion = Convert.ToInt32(tv);
        }
      } catch {
        userId = -1;
        return false;
      }
      return true;
    }

    public bool IsTokenValid(string token, out int userId) {
      return IsTokenValid(token, out userId, out _);
    }
  }
}
