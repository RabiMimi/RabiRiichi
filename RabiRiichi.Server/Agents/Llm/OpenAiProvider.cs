using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
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
      try {
        var url = $"{settings.BaseUrl}/v1/chat/completions";
        var body = new JsonObject {
          ["model"] = settings.Model,
          ["max_tokens"] = maxOutputTokens,
          ["temperature"] = 1.0,
          ["messages"] = BuildMessages(messages),
        };
        ApplyThinking(body, settings.Model, settings.ThinkingLevel);

        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
          Content = JsonContent.Create(body),
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {settings.ApiToken}");

        using var resp = await http.SendAsync(req, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) {
          return LlmResult.Fail($"HTTP {(int)resp.StatusCode}: {Truncate(text)}");
        }
        return ParseContent(text);
      } catch (OperationCanceledException) {
        return LlmResult.Fail("timeout");
      } catch (Exception e) {
        return LlmResult.Fail(e.Message);
      }
    }

    private static JsonArray BuildMessages(IReadOnlyList<LlmMessage> messages) {
      var arr = new JsonArray();
      foreach (var m in messages) {
        arr.Add(new JsonObject {
          ["role"] = RoleName(m.Role),
          ["content"] = m.Content,
        });
      }
      return arr;
    }

    private static string RoleName(LlmRole role) => role switch {
      LlmRole.System => "system",
      LlmRole.Assistant => "assistant",
      _ => "user",
    };

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

    /// <summary> Extracts <c>choices[0].message.content</c> from the response. </summary>
    public static LlmResult ParseContent(string json) {
      try {
        var node = JsonNode.Parse(json);
        var content = node?["choices"]?[0]?["message"]?["content"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(content)) {
          return LlmResult.Fail("empty response");
        }
        return LlmResult.Ok(content);
      } catch (Exception e) {
        return LlmResult.Fail($"parse error: {e.Message}");
      }
    }

    private static string Truncate(string s) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= 200 ? s : s[..200]);
  }
}
