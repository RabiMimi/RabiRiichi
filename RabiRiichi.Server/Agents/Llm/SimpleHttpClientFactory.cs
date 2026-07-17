using System.Net.Http;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// A tiny <see cref="IHttpClientFactory"/> backed by one shared, long-lived
  /// <see cref="HttpClient"/> — a safe default when the app has not registered
  /// the real factory via DI. Per-request timeouts are applied through a
  /// cancellation token by callers, not via <see cref="HttpClient.Timeout"/>.
  /// </summary>
  public sealed class SimpleHttpClientFactory : IHttpClientFactory {
    private static readonly HttpClient Shared = new() {
      // Effectively no client-level timeout; callers pass a CancellationToken.
      Timeout = System.Threading.Timeout.InfiniteTimeSpan,
    };

    public HttpClient CreateClient(string name) => Shared;
  }
}
