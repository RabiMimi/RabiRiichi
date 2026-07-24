using RabiRiichi.Server.Generated.Rpc;
using System;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Hardcoded server-side guardrails for LLM interactions. Kept in one place so
  /// they are easy to tune and reason about. No env configuration for v1.
  /// </summary>
  public static class LlmLimits {
    /// <summary> Per-request wall-clock budget for a normal in-game decision. </summary>
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    /// <summary> Budget for the one-shot validation ping when adding the AI. </summary>
    public static readonly TimeSpan ValidationTimeout = TimeSpan.FromSeconds(15);

    /// <summary> Max output tokens for an in-game decision. </summary>
    public const int MaxDecisionTokens = 512;

    /// <summary>
    /// Max output tokens for the validation ping. Kept small but not tiny:
    /// thinking models need a little headroom to emit visible text after any
    /// internal reasoning, otherwise a 200 comes back with no output.
    /// </summary>
    public const int MaxValidationTokens = 32;

    /// <summary>
    /// Max number of buffered event-delta lines kept per LLM agent. Guards
    /// against unbounded memory if the model repeatedly fails/falls back.
    /// </summary>
    public const int MaxTranscriptLines = 400;
  }



  /// <summary>
  /// How much internal "thinking"/reasoning a provider should do before it
  /// answers. <see cref="Minimal"/> is the provider's lowest/off setting and is
  /// the default so the fast in-game AI is unaffected; the Arena benchmark opts
  /// into higher levels per model. Providers map these to their own knobs
  /// (Gemini <c>thinking_level</c>, OpenAI <c>reasoning_effort</c>, DeepSeek
  /// <c>thinking.type</c>).
  /// </summary>
  public enum LlmThinkingLevel {
    Minimal,
    Low,
    Medium,
    High,
  }

  /// <summary>
  /// A validated, immutable view of an <see cref="LlmAiConfig"/> plus derived
  /// values (resolved base URL, display name, normalized language). Construct via
  /// <see cref="FromProto"/> which enforces required fields.
  /// </summary>
  public sealed class LlmSettings {
    public LlmProvider Provider { get; }
    public string ApiToken { get; }
    public string BaseUrl { get; }
    public string Model { get; }
    public string Language { get; }
    public LlmPromptTemplate PromptTemplate { get; }

    /// <summary>
    /// The custom display name the user chose, or empty if none. When empty the
    /// AI's broadcast nickname is a client-localized sentinel (see
    /// <see cref="LlmDisplayName.NicknameFor"/>) so each viewer sees the name in
    /// their own UI language — the server never invents a human-facing name.
    /// </summary>
    public string CustomDisplayName { get; }

    /// <summary>
    /// Desired provider reasoning/thinking level. Defaults to
    /// <see cref="LlmThinkingLevel.Minimal"/> (unchanged in-game behavior); the
    /// Arena passes a higher level per roster entry.
    /// </summary>
    public LlmThinkingLevel ThinkingLevel { get; }

    private LlmSettings(LlmProvider provider, string apiToken, string baseUrl,
                        string model, string language, string customDisplayName,
                        LlmPromptTemplate promptTemplate,
                        LlmThinkingLevel thinkingLevel) {
      Provider = provider;
      ApiToken = apiToken;
      BaseUrl = baseUrl;
      Model = model;
      Language = language;
      CustomDisplayName = customDisplayName;
      PromptTemplate = promptTemplate;
      ThinkingLevel = thinkingLevel;
    }

    /// <summary>
    /// Validates and normalizes a proto config. Returns null and sets
    /// <paramref name="error"/> (a short, client-facing reason) on invalid input.
    /// This does NOT contact the provider — see <c>LlmValidator</c> for that.
    ///
    /// <paramref name="thinkingLevel"/> defaults to
    /// <see cref="LlmThinkingLevel.Minimal"/> so existing callers (the in-game
    /// AI) keep the current lowest-thinking behavior; the Arena passes an
    /// explicit level per roster entry.
    /// </summary>
    public static LlmSettings FromProto(
        LlmAiConfig config, out string error,
        LlmThinkingLevel thinkingLevel = LlmThinkingLevel.Minimal) {
      error = null;
      if (config == null) {
        error = "missing";
        return null;
      }
      if (config.Provider is not (LlmProvider.Openai or LlmProvider.Gemini)) {
        error = "provider";
        return null;
      }
      if (string.IsNullOrWhiteSpace(config.ApiToken)) {
        error = "token";
        return null;
      }
      if (string.IsNullOrWhiteSpace(config.Model)) {
        error = "model";
        return null;
      }

      var baseUrl = string.IsNullOrWhiteSpace(config.BaseUrl)
          ? DefaultBaseUrl(config.Provider)
          : config.BaseUrl.Trim().TrimEnd('/');
      if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) ||
          (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) {
        error = "url";
        return null;
      }

      var language = AiLocalization.NormalizeLanguage(config.Language);
      var customName = string.IsNullOrWhiteSpace(config.DisplayName)
          ? "" : config.DisplayName.Trim();
      var promptTemplate = config.PromptTemplate switch {
        LlmPromptTemplate.Unspecified => LlmPromptTemplate.CuteJk,
        LlmPromptTemplate.CuteJk => LlmPromptTemplate.CuteJk,
        LlmPromptTemplate.Mesugaki => LlmPromptTemplate.Mesugaki,
        _ => LlmPromptTemplate.Unspecified,
      };
      if (promptTemplate == LlmPromptTemplate.Unspecified) {
        error = "prompt_template";
        return null;
      }

      return new LlmSettings(config.Provider, config.ApiToken.Trim(), baseUrl,
          config.Model.Trim(), language, customName, promptTemplate, thinkingLevel);
    }

    /// <summary> The default API base URL for a provider. </summary>
    public static string DefaultBaseUrl(LlmProvider provider) => provider switch {
      LlmProvider.Openai => "https://api.openai.com",
      LlmProvider.Gemini => "https://generativelanguage.googleapis.com",
      _ => null,
    };
  }
}
