using RabiRiichi.Server.Generated.Rpc;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Bridges the LLM AI's broadcast nickname and client-side localization.
  ///
  /// Human-facing names are localized on the CLIENT (per each viewer's UI
  /// language), so the server does not invent a human-facing name. When the user
  /// supplies a custom display name we broadcast it verbatim; otherwise we
  /// broadcast a compact sentinel nickname of the form <c>@llm:{provider}</c>
  /// (e.g. <c>@llm:gemini</c>). The client detects the <c>@llm:</c> prefix and
  /// renders a localized label (e.g. "Gemi狸" in zhs), falling back to a generic
  /// LLM label for unknown providers.
  /// </summary>
  public static class LlmDisplayName {
    /// <summary> Prefix marking a nickname the client must localize. </summary>
    public const string SentinelPrefix = "@llm:";

    /// <summary>
    /// The nickname to broadcast: the custom name if provided, else a
    /// provider sentinel the client localizes.
    /// </summary>
    public static string NicknameFor(LlmSettings settings) {
      if (!string.IsNullOrWhiteSpace(settings.CustomDisplayName)) {
        return settings.CustomDisplayName;
      }
      return SentinelPrefix + ProviderTag(settings.Provider);
    }

    /// <summary> Short, stable provider tag used in the sentinel. </summary>
    public static string ProviderTag(LlmProvider provider) => provider switch {
      LlmProvider.Gemini => "gemini",
      LlmProvider.Openai => "openai",
      _ => "generic",
    };
  }
}
