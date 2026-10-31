using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RabiRiichi.Server.Agents.Llm;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Native Anthropic Messages API (<c>POST /v1/messages</c>) provider for Arena
  /// evaluations, supporting Claude models and optional extended thinking budgets.
  /// </summary>
  public sealed class AnthropicProvider(
      HttpClient http,
      string baseUrl,
      string apiKey,
      string model,
      LlmThinkingLevel thinkingLevel = LlmThinkingLevel.Minimal,
      double? temperature = null) : ILlmProvider {
    public const string DefaultBaseUrl = "https://api.anthropic.com";
    private const string AnthropicVersion = "2023-06-01";

    private readonly HttpClient http = http ?? throw new ArgumentNullException(nameof(http));
    private readonly string baseUrl = string.IsNullOrWhiteSpace(baseUrl)
        ? DefaultBaseUrl
        : baseUrl.Trim().TrimEnd('/');
    private readonly string apiKey = apiKey ?? "";
    private readonly string model = model ?? "";
    private readonly LlmThinkingLevel thinkingLevel = thinkingLevel;
    private readonly double? temperature = temperature;

    public ProtoLlmProvider Provider => ProtoLlmProvider.Unspecified;

    public async Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken) {
      try {
        var body = BuildRequestBody(messages, maxOutputTokens, model, thinkingLevel, temperature);
        var url = $"{baseUrl}/v1/messages";

        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
          Content = JsonContent.Create(body),
        };
        req.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        req.Headers.TryAddWithoutValidation("anthropic-version", AnthropicVersion);

        using var resp = await http.SendAsync(req, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        if (!resp.IsSuccessStatusCode) {
          return LlmResult.Fail($"HTTP {(int)resp.StatusCode}: {OpenAiCompatible.Truncate(text)}");
        }
        return ParseResponse(text);
      } catch (OperationCanceledException) {
        return LlmResult.Fail("timeout");
      } catch (Exception ex) {
        return LlmResult.Fail(ex.Message);
      }
    }

    public static JsonObject BuildRequestBody(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        string model,
        LlmThinkingLevel thinkingLevel = LlmThinkingLevel.Minimal,
        double? temperature = null) {
      var systemSb = new StringBuilder();
      var chatMessages = new JsonArray();
      string lastRole = null;
      StringBuilder pendingContent = null;

      void FlushPending() {
        if (lastRole != null && pendingContent != null) {
          chatMessages.Add(new JsonObject {
            ["role"] = lastRole,
            ["content"] = pendingContent.ToString(),
          });
        }
        lastRole = null;
        pendingContent = null;
      }

      foreach (var m in messages) {
        if (m.Role == LlmRole.System) {
          if (systemSb.Length > 0) systemSb.Append("\n\n");
          systemSb.Append(m.Content);
          continue;
        }
        string role = m.Role == LlmRole.Assistant ? "assistant" : "user";
        if (role == lastRole && pendingContent != null) {
          pendingContent.Append("\n\n").Append(m.Content);
        } else {
          FlushPending();
          lastRole = role;
          pendingContent = new StringBuilder(m.Content ?? "");
        }
      }
      FlushPending();

      if (chatMessages.Count == 0) {
        chatMessages.Add(new JsonObject {
          ["role"] = "user",
          ["content"] = "Choose a legal action.",
        });
      }

      int thinkingBudget = ThinkingBudgetTokens(thinkingLevel);
      int effectiveMaxTokens = Math.Max(64, maxOutputTokens);
      if (thinkingBudget > 0 && effectiveMaxTokens <= thinkingBudget + 256) {
        effectiveMaxTokens = thinkingBudget + 512;
      }

      var body = new JsonObject {
        ["model"] = model,
        ["max_tokens"] = effectiveMaxTokens,
        ["messages"] = chatMessages,
      };
      if (systemSb.Length > 0) {
        body["system"] = systemSb.ToString();
      }
      if (thinkingBudget > 0) {
        // Anthropic extended thinking requires temperature == 1 (or omitted).
        body["thinking"] = new JsonObject {
          ["type"] = "enabled",
          ["budget_tokens"] = thinkingBudget,
        };
      } else if (temperature is double temp) {
        body["temperature"] = Math.Clamp(temp, 0.0, 1.0);
      }
      return body;
    }

    internal static int ThinkingBudgetTokens(LlmThinkingLevel level) => level switch {
      LlmThinkingLevel.Low => 1024,
      LlmThinkingLevel.Medium => 2048,
      LlmThinkingLevel.High => 4096,
      _ => 0,
    };

    public static LlmResult ParseResponse(string json) {
      try {
        var node = JsonNode.Parse(json);
        if (node == null) return LlmResult.Fail("empty response");

        var contentArr = node["content"]?.AsArray();
        var textSb = new StringBuilder();
        var thoughtSb = new StringBuilder();
        if (contentArr != null) {
          foreach (var block in contentArr) {
            var type = block?["type"]?.GetValue<string>();
            if (type == "text") {
              var t = block?["text"]?.GetValue<string>();
              if (!string.IsNullOrEmpty(t)) textSb.Append(t);
            } else if (type == "thinking") {
              var th = block?["thinking"]?.GetValue<string>();
              if (!string.IsNullOrEmpty(th)) {
                if (thoughtSb.Length > 0) thoughtSb.Append('\n');
                thoughtSb.Append(th);
              }
            }
          }
        }

        string text = textSb.ToString();
        if (string.IsNullOrWhiteSpace(text)) {
          return LlmResult.Fail("empty response");
        }

        var usage = node["usage"];
        int inputTokens = SafeInt(usage?["input_tokens"]) +
                          SafeInt(usage?["cache_creation_input_tokens"]) +
                          SafeInt(usage?["cache_read_input_tokens"]);
        int outputTokens = SafeInt(usage?["output_tokens"]);
        string thought = thoughtSb.Length > 0 ? thoughtSb.ToString() : null;
        return LlmResult.Ok(text, inputTokens, outputTokens, 0, thought);
      } catch (Exception ex) {
        return LlmResult.Fail($"parse error: {ex.Message}");
      }
    }

    private static int SafeInt(JsonNode node) {
      try {
        return node != null ? Math.Max(0, node.GetValue<int>()) : 0;
      } catch {
        return 0;
      }
    }
  }
}
