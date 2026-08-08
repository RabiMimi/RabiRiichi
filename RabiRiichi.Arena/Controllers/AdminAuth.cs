using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using RabiRiichi.Arena.Config;

namespace RabiRiichi.Arena.Controllers {
  /// <summary>
  /// Gates an admin action/controller behind the single admin password from the
  /// config (ARENA_DESIGN.md §12b). There is no user system and no JWT: the
  /// caller sends the password in the <c>X-Admin-Password</c> header, which is
  /// compared to <c>ArenaConfig.AdminPassword</c> using a constant-time
  /// comparison. On mismatch, an empty header, OR an empty/unset configured
  /// password (fail closed), the request is rejected with 401.
  ///
  /// Applied as a type/method attribute; the configured password is read at
  /// request time from the live <see cref="ArenaConfigProvider"/> (resolved from
  /// DI) so config hot-reloads take effect without a restart.
  /// </summary>
  [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
  public sealed class AdminAuthAttribute : Attribute, IAuthorizationFilter {
    /// <summary>Header carrying the admin password.</summary>
    public const string HeaderName = "X-Admin-Password";

    public void OnAuthorization(AuthorizationFilterContext context) {
      var provider = context.HttpContext.RequestServices
          .GetService(typeof(ArenaConfigProvider)) as ArenaConfigProvider;
      var configured = provider?.Current?.AdminPassword;
      context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var supplied);

      if (!IsAuthorized(configured, supplied.ToString())) {
        context.Result = new UnauthorizedResult();
      }
    }

    /// <summary>
    /// Constant-time check that <paramref name="supplied"/> matches the configured
    /// password. Fails closed: an empty/whitespace configured password OR an
    /// empty supplied password is never authorized. Extracted (pure) so it is
    /// unit-testable without the ASP.NET pipeline.
    /// </summary>
    public static bool IsAuthorized(string configured, string supplied) {
      if (string.IsNullOrWhiteSpace(configured)
          || string.IsNullOrEmpty(supplied)) {
        return false;
      }
      var a = Encoding.UTF8.GetBytes(configured);
      var b = Encoding.UTF8.GetBytes(supplied);
      return CryptographicOperations.FixedTimeEquals(a, b);
    }
  }
}
