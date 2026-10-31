using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using RabiRiichi.Arena.Agents;
using RabiRiichi.Arena.Storage;
using RabiRiichi.Server.Agents.Llm;
using RabiRiichi.Server.Generated.Rpc;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Arena.Eval {
  /// <summary>
  /// Resolves an <see cref="ArenaConfig.ModelConfig"/> into a concrete
  /// <see cref="ILlmProvider"/> (OpenAI / OpenAI-compatible, Gemini, xAI Grok,
  /// or Anthropic Claude) and provides a live diagnostic test helper for the
  /// Web UI Configuration Studio.
  /// </summary>
  public static class ArenaProviderResolver {
    private static readonly HttpClient SharedHttp = new() {
      Timeout = Timeout.InfiniteTimeSpan,
    };

    public sealed record ModelTestOutcome(
        bool Ok,
        long LatencyMs,
        int? ParsedAction,
        string Rationale,
        string RawContent,
        string ReasoningContent,
        int PromptTokens,
        int CompletionTokens,
        int ReasoningTokens,
        string Error);

    public static EvalRoom.LlmProviderResolver Create(
        ILlmProviderFactory factory,
        IHttpClientFactory httpClientFactory) {
      return model => Resolve(
          model,
          httpClientFactory?.CreateClient("llm"),
          factory);
    }

    public static ILlmProvider Resolve(
        ArenaConfig.ModelConfig model,
        HttpClient http = null,
        ILlmProviderFactory factory = null) {
      if (model == null) throw new ArgumentNullException(nameof(model));
      if (model.IsBaseline) {
        throw new ArgumentException(
            $"Baseline model '{model.Id}' uses ArenaBaselineAI, not ILlmProvider.", nameof(model));
      }

      var client = http ?? SharedHttp;
      var thinking = ParseThinkingLevel(model.ThinkingLevel);
      var providerName = (model.Provider ?? "").Trim().ToLowerInvariant();

      if (providerName == "anthropic") {
        return new AnthropicProvider(
            client,
            model.BaseUrl,
            model.ApiKey ?? "",
            (model.Model ?? "").Trim(),
            thinking,
            model.Temperature);
      }

      var protoProvider = ParseProtoProvider(providerName);
      var hasCustomBaseUrl = !string.IsNullOrWhiteSpace(model.BaseUrl);
      var token = !string.IsNullOrWhiteSpace(model.ApiKey)
          ? model.ApiKey.Trim()
          : (hasCustomBaseUrl ? "local-no-key" : "");
      if (string.IsNullOrEmpty(token)) {
        throw new ArgumentException(
            $"API key is required for model '{model.Id}' (provider '{model.Provider}').",
            nameof(model));
      }

      var protoCfg = new LlmAiConfig {
        Provider = protoProvider,
        ApiToken = token,
        BaseUrl = hasCustomBaseUrl ? model.BaseUrl.Trim().TrimEnd('/') : "",
        Model = (model.Model ?? "").Trim(),
        Language = "en",
        DisplayName = model.DisplayName ?? "",
      };
      var settings = LlmSettings.FromProto(protoCfg, out var err, thinking)
          ?? throw new ArgumentException(
              $"Invalid LLM settings for model '{model.Id}': {err}", nameof(model));

      // Arena sends condensed, retry-aware message arrays, so Gemini uses stateless
      // requests unless a custom test factory overrides creation.
      if (factory != null && factory is not LlmProviderFactory) {
        return factory.Create(settings);
      }

      return protoProvider switch {
        ProtoLlmProvider.Openai => new OpenAiProvider(client, settings),
        ProtoLlmProvider.Gemini => new GeminiProvider(client, settings, stateful: false),
        ProtoLlmProvider.Grok => new GrokProvider(client, settings),
        _ => throw new ArgumentException($"Unsupported provider '{model.Provider}'.", nameof(model)),
      };
    }

    public static ProtoLlmProvider ParseProtoProvider(string provider) =>
        (provider ?? "").Trim().ToLowerInvariant() switch {
          "openai" => ProtoLlmProvider.Openai,
          "gemini" => ProtoLlmProvider.Gemini,
          "grok" or "xai" => ProtoLlmProvider.Grok,
          _ => throw new ArgumentException(
              $"Unknown LLM provider '{provider}' (expected 'openai', 'gemini', 'grok', or 'anthropic').",
              nameof(provider)),
        };

    public static LlmThinkingLevel ParseThinkingLevel(string raw) =>
        (raw ?? "minimal").Trim().ToLowerInvariant() switch {
          "low" => LlmThinkingLevel.Low,
          "medium" => LlmThinkingLevel.Medium,
          "high" => LlmThinkingLevel.High,
          _ => LlmThinkingLevel.Minimal,
        };

    /// <summary>
    /// Runs a lightweight, realistic 2-choice Riichi Mahjong probe against the
    /// configured provider to verify credentials, endpoint URL, model name, and
    /// JSON contract compliance.
    /// </summary>
    public static async Task<ModelTestOutcome> TestConnectionAsync(
        ArenaConfig.ModelConfig model,
        int timeoutSeconds = 25,
        HttpClient http = null,
        ILlmProviderFactory factory = null,
        CancellationToken ct = default) {
      if (model == null) {
        return new(false, 0, null, null, null, null, 0, 0, 0, "Model configuration is missing.");
      }
      if (model.IsBaseline) {
        return new(true, 0, 0, "Local rule-based baseline engine ready (no API call needed).",
            "{\"action\":0,\"rationale\":\"baseline ready\"}", null, 0, 0, 0, null);
      }
      if (string.IsNullOrWhiteSpace(model.Model)) {
        return new(false, 0, null, null, null, null, 0, 0, 0, "Model identifier is required.");
      }

      ILlmProvider provider;
      try {
        provider = Resolve(model, http, factory);
      } catch (Exception ex) {
        return new(false, 0, null, null, null, null, 0, 0, 0, ex.Message);
      }

      var messages = new List<LlmMessage> {
        LlmMessage.System(
            "You are playing Japanese Riichi Mahjong. Respond with ONLY a single JSON object: " +
            "{\"action\": <integer id>, \"rationale\": \"<short reason>\"}."),
        LlmMessage.User(
            "East 1, Turn 4. Hand: 2m 3m 4m 5p 6p 7p 2s 3s 4s 6s 7s 8s 5z (drawn: 1z).\n" +
            "Legal actions:\n  [0] Discard 5z\n  [1] Discard 1z\n" +
            "Choose the best action id (0 or 1) in JSON."),
      };

      int maxTokens = Math.Clamp(model.MaxOutputTokens ?? 512, 128, 4096);
      using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
      cts.CancelAfter(TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120)));

      var sw = Stopwatch.StartNew();
      var res = await provider.CompleteAsync(messages, maxTokens, cts.Token);
      sw.Stop();

      if (!res.Success) {
        return new(false, sw.ElapsedMilliseconds, null, null, res.Content, res.ReasoningContent,
            res.PromptTokens, res.CompletionTokens, res.ReasoningTokens,
            res.Error ?? "Provider request failed.");
      }

      var parsed = ArenaLlmResponse.Parse(res.Content);
      bool validChoice = parsed.HasAction && parsed.ActionId is 0 or 1;
      return new(
          validChoice,
          sw.ElapsedMilliseconds,
          parsed.ActionId,
          parsed.Rationale,
          res.Content,
          res.ReasoningContent,
          res.PromptTokens,
          res.CompletionTokens,
          res.ReasoningTokens,
          validChoice ? null : $"Model responded, but JSON action was not 0 or 1 (got: {OpenAiCompatible.Truncate(res.Content)}).");
    }
  }
}
