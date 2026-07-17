using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Talks to Google's Gemini <c>generateContent</c> API. System messages are
  /// mapped to <c>system_instruction</c>; user/assistant turns become
  /// <c>contents</c> with roles user/model.
  /// </summary>
  public sealed class GeminiProvider(HttpClient http, LlmSettings settings) : ILlmProvider {
    private readonly HttpClient http = http;
    private readonly LlmSettings settings = settings;

    public ProtoLlmProvider Provider => ProtoLlmProvider.Gemini;

    public async Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken) {
      try {
        // Key is passed as a query param; avoid logging the full URL anywhere.
        var url = $"{settings.BaseUrl}/v1beta/models/{settings.Model}:generateContent" +
                  $"?key={Uri.EscapeDataString(settings.ApiToken)}";
        var body = BuildBody(messages, maxOutputTokens);

        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
          Content = JsonContent.Create(body),
        };
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

    /// <summary> Builds the generateContent request body. </summary>
    public static JsonObject BuildBody(IReadOnlyList<LlmMessage> messages, int maxOutputTokens) {
      var contents = new JsonArray();
      JsonObject systemInstruction = null;
      foreach (var m in messages) {
        if (m.Role == LlmRole.System) {
          // Concatenate multiple system messages into one instruction.
          systemInstruction ??= new JsonObject { ["parts"] = new JsonArray() };
          ((JsonArray)systemInstruction["parts"]).Add(new JsonObject { ["text"] = m.Content });
          continue;
        }
        contents.Add(new JsonObject {
          ["role"] = m.Role == LlmRole.Assistant ? "model" : "user",
          ["parts"] = new JsonArray { new JsonObject { ["text"] = m.Content } },
        });
      }

      var body = new JsonObject {
        ["contents"] = contents,
        ["generationConfig"] = new JsonObject {
          ["maxOutputTokens"] = maxOutputTokens,
          ["temperature"] = 0.7,
        },
      };
      if (systemInstruction != null) {
        body["system_instruction"] = systemInstruction;
      }
      return body;
    }

    /// <summary> Extracts <c>candidates[0].content.parts[0].text</c>. </summary>
    public static LlmResult ParseContent(string json) {
      try {
        var node = JsonNode.Parse(json);
        var parts = node?["candidates"]?[0]?["content"]?["parts"]?.AsArray();
        if (parts == null || parts.Count == 0) {
          return LlmResult.Fail("empty response");
        }
        var sb = new System.Text.StringBuilder();
        foreach (var part in parts) {
          var t = part?["text"]?.GetValue<string>();
          if (!string.IsNullOrEmpty(t)) {
            sb.Append(t);
          }
        }
        var content = sb.ToString();
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
