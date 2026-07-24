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
  /// Arena owns decision timeout / max-tokens / thinking (applied in
  /// <c>ArenaLlmAI</c>), so the provider itself only needs provider kind, token,
  /// base URL, and model name. The prompt template is irrelevant here —
  /// <c>ArenaLlmAI</c> supplies its own system prompt — but
  /// <see cref="LlmSettings.FromProto"/> requires a concrete one, so we pass
  /// CUTE_JK to satisfy validation.
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
      var settings = LlmSettings.FromProto(proto, out var error);
      if (settings == null) {
        throw new InvalidOperationException(
            $"Model '{model.Id}' has an invalid LLM config: {error}.");
      }
      return settings;
    }

    private static LlmProvider ParseProvider(string provider) => provider switch {
      "openai" => LlmProvider.Openai,
      "gemini" => LlmProvider.Gemini,
      _ => throw new InvalidOperationException(
          $"Unsupported LLM provider '{provider}' (expected openai or gemini)."),
    };
  }
}
