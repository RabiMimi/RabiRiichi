using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary> A raw <c>/v1/chat/completions</c> exchange. </summary>
  public readonly struct ChatCompletionExchange(
      bool success, HttpStatusCode status, string body, string error) {
    /// <summary> Whether the HTTP call itself succeeded (2xx). </summary>
    public bool Success { get; } = success;
    public HttpStatusCode Status { get; } = status;

    /// <summary> Raw response body; empty when the call threw. </summary>
    public string Body { get; } = body;

    /// <summary> Transport-level failure description, or null. </summary>
    public string Error { get; } = error;
  }

  /// <summary>
  /// Wire helpers shared by every provider that speaks the OpenAI
  /// <c>/v1/chat/completions</c> dialect.
  ///
  /// Several vendors are drop-in compatible on the transport and the response
  /// envelope while differing in a handful of request fields - xAI deprecates
  /// <c>max_tokens</c> in favour of <c>max_completion_tokens</c>, DeepSeek uses
  /// <c>thinking.type</c> instead of <c>reasoning_effort</c>. Keeping the
  /// identical parts here means a new vendor is a small subclassed difference
  /// rather than a second copy of the HTTP, auth, and parsing code.
  /// </summary>
  public static class OpenAiCompatible {
    /// <summary> Renders the conversation as the standard messages array. </summary>
    public static JsonArray BuildMessages(IReadOnlyList<LlmMessage> messages) {
      var arr = new JsonArray();
      foreach (var m in messages) {
        arr.Add(new JsonObject {
          ["role"] = RoleName(m.Role),
          ["content"] = m.Content,
        });
      }
      return arr;
    }

    public static string RoleName(LlmRole role) => role switch {
      LlmRole.System => "system",
      LlmRole.Assistant => "assistant",
      _ => "user",
    };

    /// <summary>
    /// POSTs a chat-completions body with bearer auth. Returns the raw exchange
    /// so callers can inspect a rejection before deciding what to do with it.
    /// </summary>
    public static async Task<ChatCompletionExchange> PostAsync(
        HttpClient http, string baseUrl, string apiToken, JsonObject body,
        CancellationToken cancellationToken) {
      try {
        var url = $"{baseUrl}/v1/chat/completions";
        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
          Content = JsonContent.Create(body),
        };
        req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiToken}");

        using var resp = await http.SendAsync(req, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);
        return new ChatCompletionExchange(
            resp.IsSuccessStatusCode, resp.StatusCode, text, null);
      } catch (OperationCanceledException) {
        return new ChatCompletionExchange(false, default, null, "timeout");
      } catch (Exception e) {
        return new ChatCompletionExchange(false, default, null, e.Message);
      }
    }

    /// <summary> Turns a raw exchange into a result, parsing the body on 2xx. </summary>
    public static LlmResult ToResult(ChatCompletionExchange exchange) {
      if (exchange.Error != null) {
        return LlmResult.Fail(exchange.Error);
      }
      if (!exchange.Success) {
        return LlmResult.Fail(
            $"HTTP {(int)exchange.Status}: {Truncate(exchange.Body)}");
      }
      return ParseContent(exchange.Body);
    }

    /// <summary> Extracts <c>choices[0].message.content</c>, reasoning text, and token usage from the response. </summary>
    public static LlmResult ParseContent(string json) {
      try {
        var node = JsonNode.Parse(json);
        var msg = node?["choices"]?[0]?["message"];
        var content = AsString(msg?["content"]);
        if (string.IsNullOrWhiteSpace(content)) {
          return LlmResult.Fail("empty response");
        }
        var reasoning = AsString(msg?["reasoning_content"]) ?? AsString(msg?["reasoning"]);
        var usage = node?["usage"];
        int promptTokens = AsInt(usage?["prompt_tokens"]);
        int completionTokens = AsInt(usage?["completion_tokens"]);
        int reasoningTokens = AsInt(usage?["completion_tokens_details"]?["reasoning_tokens"]);
        return LlmResult.Ok(content, promptTokens, completionTokens, reasoningTokens, reasoning);
      } catch (Exception e) {
        return LlmResult.Fail($"parse error: {e.Message}");
      }
    }

    private static string AsString(JsonNode node) {
      try {
        return node?.GetValue<string>();
      } catch {
        return null;
      }
    }

    private static int AsInt(JsonNode node) {
      try {
        return node != null ? Math.Max(0, node.GetValue<int>()) : 0;
      } catch {
        return 0;
      }
    }

    public static string Truncate(string s) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= 200 ? s : s[..200]);
  }
}
