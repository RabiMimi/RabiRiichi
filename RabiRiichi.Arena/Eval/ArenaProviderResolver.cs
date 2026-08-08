using System;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// Maps an Arena <see cref="ArenaConfig.ModelConfig"/> to a server
  /// <see cref="ILlmProvider"/> via <see cref="LlmSettings"/> + an
  /// <see cref="ILlmProviderFactory"/>. This is the resolver
  /// <see cref="EvalRoom"/> calls for LLM seats (baseline seats never reach it).
  ///
  /// Arena owns decision timeout / max-tokens, and picks the per-model thinking
  /// level here (threaded into <see cref="LlmSettings"/> so the providers apply
  /// it). The provider otherwise only needs provider kind, token, base URL, and
  /// model name. The prompt template is irrelevant here — <c>ArenaLlmAI</c>
  /// supplies its own system prompt — but <see cref="LlmSettings.FromProto"/>
  /// requires a concrete one, so we pass CUTE_JK to satisfy validation.
  /// </summary>
  public static class ArenaProviderResolver {
    public static EvalRoom.LlmProviderResolver Create(ILlmProviderFactory factory) {
      if (factory == null) {
        throw new ArgumentNullException(nameof(factory));
      }
      return model => {
        var settings = ToSettings(model);
        return factory.Create(settings);
      };
    }

    private static LlmSettings ToSettings(ArenaConfig.ModelConfig model) {
      var proto = new LlmAiConfig {
        Provider = ParseProvider(model.Provider),
        ApiToken = model.ApiKey ?? "",
        BaseUrl = model.BaseUrl ?? "",
        Model = model.Model ?? "",
        PromptTemplate = LlmPromptTemplate.CuteJk,
      };
      var settings = LlmSettings.FromProto(
          proto, out var error, ParseThinkingLevel(model.ThinkingLevel));
      if (settings == null) {
        throw new InvalidOperationException(
            $"Model '{model.Id}' has an invalid LLM config: {error}.");
      }
      return settings;
    }

    /// <summary>
    /// Maps the config string ("minimal"/"low"/"medium"/"high", case-insensitive)
    /// to <see cref="LlmThinkingLevel"/>. Empty/unrecognized falls back to
    /// <see cref="LlmThinkingLevel.High"/> — the config default (benchmark models
    /// should think; config validation rejects other non-empty values upstream).
    /// </summary>
    public static LlmThinkingLevel ParseThinkingLevel(string level) =>
        (level ?? "").Trim().ToLowerInvariant() switch {
          "minimal" => LlmThinkingLevel.Minimal,
          "low" => LlmThinkingLevel.Low,
          "medium" => LlmThinkingLevel.Medium,
          _ => LlmThinkingLevel.High,
        };

    private static LlmProvider ParseProvider(string provider) => provider switch {
      "openai" => LlmProvider.Openai,
      "gemini" => LlmProvider.Gemini,
      _ => throw new InvalidOperationException(
          $"Unsupported LLM provider '{provider}' (expected openai or gemini)."),
    };
  }
}
