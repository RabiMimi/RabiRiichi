using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Talks to any OpenAI-compatible <c>/v1/chat/completions</c> endpoint. Also
  /// works with self-hosted / proxy servers that speak the same format.
  /// </summary>
  public sealed class OpenAiProvider(HttpClient http, LlmSettings settings) : ILlmProvider {
    private readonly HttpClient http = http;
    private readonly LlmSettings settings = settings;

    public ProtoLlmProvider Provider => ProtoLlmProvider.Openai;

    public async Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken) {
      string tokenKey = UsesMaxCompletionTokens(settings.Model)
          ? "max_completion_tokens"
          : "max_tokens";
      var body = new JsonObject {
        ["model"] = settings.Model,
        [tokenKey] = maxOutputTokens,
        ["temperature"] = 1.0,
        ["messages"] = OpenAiCompatible.BuildMessages(messages),
      };
      ApplyThinking(body, settings.Model, settings.ThinkingLevel);

      var exchange = await OpenAiCompatible.PostAsync(
          http, settings.BaseUrl, settings.ApiToken, body, cancellationToken);
      return OpenAiCompatible.ToResult(exchange);
    }

    internal static bool UsesMaxCompletionTokens(string model) {
      if (string.IsNullOrWhiteSpace(model)) return false;
      var m = model.Trim();
      return m.StartsWith("o1", StringComparison.OrdinalIgnoreCase) ||
             m.StartsWith("o3", StringComparison.OrdinalIgnoreCase) ||
             m.StartsWith("o4", StringComparison.OrdinalIgnoreCase) ||
             m.StartsWith("gpt-5", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsDeepSeekModel(string model) =>
        model?.StartsWith("deepseek-", StringComparison.OrdinalIgnoreCase) == true;

    /// <summary>
    /// Applies the requested thinking level to an OpenAI-compatible request body.
    ///
    /// DeepSeek only exposes on/off (<c>thinking.type</c>): Minimal disables it
    /// (its reasoning tokens otherwise crowd out the small JSON answer), any
    /// higher level enables it. Other OpenAI-compatible models use
    /// <c>reasoning_effort</c>, sent ONLY above Minimal — at Minimal nothing is
    /// added, so non-reasoning models (e.g. gpt-4o) are unaffected as before.
    /// </summary>
    internal static void ApplyThinking(JsonObject body, string model, LlmThinkingLevel level) {
      if (IsDeepSeekModel(model)) {
        body["thinking"] = new JsonObject {
          ["type"] = level == LlmThinkingLevel.Minimal ? "disabled" : "enabled",
        };
        return;
      }
      var effort = ReasoningEffort(level);
      if (effort != null) {
        body["reasoning_effort"] = effort;
      }
    }

    /// <summary>
    /// Maps a thinking level to OpenAI's <c>reasoning_effort</c>, or null for
    /// <see cref="LlmThinkingLevel.Minimal"/> (send nothing — safe for
    /// non-reasoning models).
    /// </summary>
    internal static string ReasoningEffort(LlmThinkingLevel level) => level switch {
      LlmThinkingLevel.Low => "low",
      LlmThinkingLevel.Medium => "medium",
      LlmThinkingLevel.High => "high",
      _ => null,
    };

    /// <summary>
    /// Extracts <c>choices[0].message.content</c>. Kept as a forwarding shim so
    /// existing callers keep working; the implementation is shared with every
    /// other OpenAI-dialect provider.
    /// </summary>
    public static LlmResult ParseContent(string json) =>
        OpenAiCompatible.ParseContent(json);
  }
}
