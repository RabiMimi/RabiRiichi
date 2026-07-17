using System.Collections.Generic;
using RabiRiichi.Server.Generated.Messages;
using RabiRiichi.Server.Generated.Rpc;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// PROMPT-ONLY localized names for AI players, in the language the LLM is asked
  /// to respond in. This lets the prompt refer to opponents by a natural name
  /// (e.g. "小和和" instead of "RULEBASED") so the LLM addresses them nicely
  /// instead of leaking internal enum names.
  ///
  /// This is deliberately the ONLY human-language localization the server does,
  /// and it exists solely because the text is embedded in the LLM prompt (the
  /// server is the only place that assembles that prompt). All player-visible UI
  /// localization is done on the client. Keep these names in sync with the web
  /// client's <c>ai.type.*</c> strings so both refer to bots identically.
  ///
  /// Languages are matched case-insensitively on a short tag prefix
  /// (e.g. "zhs", "zh", "en", "ja"). Unknown languages fall back to English.
  /// </summary>
  public static class AiLocalization {
    /// <summary> Canonical language buckets we localize into. </summary>
    public const string LangZhs = "zhs";
    public const string LangEn = "en";
    public const string LangJa = "ja";

    /// <summary>
    /// Normalizes an arbitrary language tag to one of our supported buckets.
    /// </summary>
    public static string NormalizeLanguage(string language) {
      if (string.IsNullOrWhiteSpace(language)) {
        return LangEn;
      }
      var lang = language.Trim().ToLowerInvariant();
      if (lang.StartsWith("zh")) {
        return LangZhs;
      }
      if (lang.StartsWith("ja")) {
        return LangJa;
      }
      return LangEn;
    }

    // Display names for the non-LLM AI types, per language. These MUST match the
    // web client's `ai.type.*` locale strings (src/locales/*.json) so humans and
    // the LLM refer to the same bots by the same names.
    private static readonly Dictionary<string, Dictionary<AiType, string>> AiNames =
        new() {
          [LangZhs] = new() {
            [AiType.Dummy] = "口水兔",
            [AiType.RuleBased] = "小和和",
          },
          [LangEn] = new() {
            [AiType.Dummy] = "Drooling Rabbit",
            [AiType.RuleBased] = "Nodocchi",
          },
          [LangJa] = new() {
            [AiType.Dummy] = "よだれうさぎ",
            [AiType.RuleBased] = "のどっち",
          },
        };

    // PROMPT-ONLY names for an LLM AI that has no custom display name, so the
    // model can refer to itself / other LLM bots naturally. These mirror the
    // labels the client localizes for the same sentinel; the client's copy is
    // authoritative for what humans see. Gemini in zhs is the product-requested
    // "Gemi狸".
    private static readonly Dictionary<string, Dictionary<LlmProvider, string>> LlmPromptNames =
        new() {
          [LangZhs] = new() {
            [LlmProvider.Gemini] = "Gemi狸",
            [LlmProvider.Openai] = "AI",
          },
          [LangEn] = new() {
            [LlmProvider.Gemini] = "Gemini",
            [LlmProvider.Openai] = "AI",
          },
          [LangJa] = new() {
            [LlmProvider.Gemini] = "ジェミたぬき",
            [LlmProvider.Openai] = "AI",
          },
        };

    /// <summary>
    /// The display name for a non-LLM AI type in the given language. Falls back
    /// to English, then to a readable form of the enum name.
    /// </summary>
    public static string AiDisplayName(AiType type, string language) {
      var lang = NormalizeLanguage(language);
      if (AiNames.TryGetValue(lang, out var byType) &&
          byType.TryGetValue(type, out var name)) {
        return name;
      }
      if (AiNames[LangEn].TryGetValue(type, out var en)) {
        return en;
      }
      return type.ToString();
    }

    /// <summary>
    /// PROMPT-ONLY name for an LLM AI (for embedding in prompts), given its
    /// custom name (may be empty), provider, and the prompt language.
    /// </summary>
    public static string LlmPromptName(string customName, LlmProvider provider, string language) {
      if (!string.IsNullOrWhiteSpace(customName)) {
        return customName;
      }
      var lang = NormalizeLanguage(language);
      if (LlmPromptNames.TryGetValue(lang, out var byProvider) &&
          byProvider.TryGetValue(provider, out var name)) {
        return name;
      }
      if (LlmPromptNames[LangEn].TryGetValue(provider, out var en)) {
        return en;
      }
      return provider.ToString();
    }
  }
}
