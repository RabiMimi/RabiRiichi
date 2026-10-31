using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RabiRiichi.Arena.Config;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// Ephemeral HMAC-SHA256 admin session manager backing HttpOnly cookies + CSRF tokens
  /// so browser clients never store passwords or tokens in localStorage/sessionStorage.
  /// </summary>
  public sealed class AdminSessionService {
    public const string CookieName = "arena_session";
    public const string CsrfHeaderName = "X-Arena-Csrf";
    private static readonly TimeSpan SessionLifetime = TimeSpan.FromHours(12);

    private readonly byte[] secretKey = RandomNumberGenerator.GetBytes(32);

    public (string CookieValue, string CsrfToken, DateTimeOffset ExpiresAt) CreateSession(string currentAdminPassword) {
      var nonceBytes = RandomNumberGenerator.GetBytes(16);
      string nonce = Convert.ToHexString(nonceBytes);
      var expiresAt = DateTimeOffset.UtcNow.Add(SessionLifetime);
      long expUnix = expiresAt.ToUnixTimeSeconds();
      string pwdTag = ComputeTag($"pwd|{currentAdminPassword}");
      string payload = $"{nonce}.{expUnix}.{pwdTag}";
      string sig = ComputeTag($"sess|{payload}");
      string csrf = ComputeTag($"csrf|{nonce}");
      return ($"{payload}.{sig}", csrf, expiresAt);
    }

    public bool TryValidateCookie(string cookieValue, string currentAdminPassword, out string csrfToken) {
      csrfToken = null;
      if (string.IsNullOrWhiteSpace(cookieValue) || string.IsNullOrWhiteSpace(currentAdminPassword)) {
        return false;
      }
      var parts = cookieValue.Split('.');
      if (parts.Length != 4) {
        return false;
      }
      string nonce = parts[0];
      if (!long.TryParse(parts[1], out long expUnix)) {
        return false;
      }
      if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expUnix) {
        return false;
      }

      string payload = $"{parts[0]}.{parts[1]}.{parts[2]}";
      string expectedSig = ComputeTag($"sess|{payload}");
      if (!FixedEquals(parts[3], expectedSig)) {
        return false;
      }

      string expectedPwdTag = ComputeTag($"pwd|{currentAdminPassword}");
      if (!FixedEquals(parts[2], expectedPwdTag)) {
        return false;
      }

      csrfToken = ComputeTag($"csrf|{nonce}");
      return true;
    }

    public static bool ValidateCsrf(string expectedCsrf, string suppliedCsrf) =>
        !string.IsNullOrEmpty(expectedCsrf) &&
        !string.IsNullOrEmpty(suppliedCsrf) &&
        FixedEquals(expectedCsrf, suppliedCsrf);

    private string ComputeTag(string data) {
      var hash = HMACSHA256.HashData(secretKey, Encoding.UTF8.GetBytes(data));
      return Convert.ToHexString(hash);
    }

    private static bool FixedEquals(string a, string b) {
      if (a == null || b == null) return false;
      var ba = Encoding.UTF8.GetBytes(a);
      var bb = Encoding.UTF8.GetBytes(b);
      return CryptographicOperations.FixedTimeEquals(ba, bb);
    }
  }

  /// <summary>
  /// Authenticates admin requests via either:
  ///  1. <c>X-Admin-Password</c> header (for programmatic/CLI/test callers), OR
  ///  2. <c>arena_session</c> HttpOnly cookie (+ <c>X-Arena-Csrf</c> header on unsafe HTTP methods).
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
  public sealed class AdminAuthAttribute : Attribute, IAuthorizationFilter {
    public const string HeaderName = "X-Admin-Password";

    public void OnAuthorization(AuthorizationFilterContext context) {
      // Honor [AllowAnonymous] on login/logout/session probe actions.
      for (int i = 0; i < context.ActionDescriptor.EndpointMetadata.Count; i++) {
        if (context.ActionDescriptor.EndpointMetadata[i] is IAllowAnonymous) {
          return;
        }
      }

      if (!IsRequestAuthorized(context.HttpContext)) {
        context.Result = new UnauthorizedResult();
      }
    }

    public static bool IsRequestAuthorized(HttpContext httpContext) {
      if (httpContext == null) return false;
      var provider = httpContext.RequestServices
          .GetService(typeof(ArenaConfigProvider)) as ArenaConfigProvider;
      var configured = provider?.Current?.AdminPassword;
      if (string.IsNullOrWhiteSpace(configured)) {
        return false;
      }

      if (httpContext.Request.Headers.TryGetValue(HeaderName, out var suppliedHeader) &&
          suppliedHeader.Count > 0 &&
          IsAuthorized(configured, suppliedHeader.ToString())) {
        return true;
      }

      var sessions = httpContext.RequestServices
          .GetService(typeof(AdminSessionService)) as AdminSessionService;
      if (sessions != null &&
          httpContext.Request.Cookies.TryGetValue(AdminSessionService.CookieName, out var cookieVal) &&
          sessions.TryValidateCookie(cookieVal, configured, out var expectedCsrf)) {
        string method = httpContext.Request.Method;
        bool safeMethod = HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method);
        if (safeMethod) {
          return true;
        }
        httpContext.Request.Headers.TryGetValue(AdminSessionService.CsrfHeaderName, out var suppliedCsrf);
        return AdminSessionService.ValidateCsrf(expectedCsrf, suppliedCsrf.ToString());
      }

      return false;
    }

    public static bool IsAuthorized(string configured, string supplied) {
      if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrEmpty(supplied)) {
        return false;
      }
      var a = Encoding.UTF8.GetBytes(configured);
      var b = Encoding.UTF8.GetBytes(supplied);
      return CryptographicOperations.FixedTimeEquals(a, b);
    }
  }
}
