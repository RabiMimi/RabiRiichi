namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Process-wide holder for the <see cref="ILlmProviderFactory"/> used by
  /// <see cref="LlmAI"/> instances. AI agents are created through a plain
  /// factory delegate (no DI scope), so this seam lets Startup inject the real
  /// factory once, and lets tests substitute a fake.
  /// </summary>
  public static class LlmRuntime {
    private static ILlmProviderFactory factory;

    /// <summary>
    /// The active factory. Defaults to a lazily-created HTTP-backed factory so
    /// the server works even if Startup forgets to configure it, but Startup
    /// should set this explicitly for proper HttpClient lifetime management.
    /// </summary>
    public static ILlmProviderFactory Factory {
      get => factory ??= new LlmProviderFactory(new SimpleHttpClientFactory());
      set => factory = value;
    }
  }
}
