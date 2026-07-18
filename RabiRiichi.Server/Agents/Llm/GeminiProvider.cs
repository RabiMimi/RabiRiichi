using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using RabiRiichi.Utils;
using ProtoLlmProvider = RabiRiichi.Server.Generated.Rpc.LlmProvider;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Talks to Google's Gemini <b>Interactions API</b>
  /// (<c>POST /v1beta/interactions</c>), the current recommended surface
  /// (generateContent is now legacy).
  ///
  /// It runs in <b>stateful</b> mode: the first turn sends the system
  /// instruction plus the conversation so far and stores it server-side; each
  /// later turn sends only the new user message and references the prior
  /// interaction via <c>previous_interaction_id</c>. The server keeps the
  /// history, which cuts request size and — per the API docs — improves implicit
  /// cache hit rates (lower cost/latency) across a game.
  ///
  /// Because state is per-conversation and a provider instance is created per LLM
  /// agent, the last interaction id is held on the instance. System messages map
  /// to <c>system_instruction</c>; user/assistant turns become <c>input</c>
  /// steps (<c>user_input</c> / <c>model_output</c>). All wire fields are
  /// snake_case (an Interactions-API convention, unlike generateContent).
  /// </summary>
  public sealed class GeminiProvider(HttpClient http, LlmSettings settings) : ILlmProvider {
    private readonly HttpClient http = http;
    private readonly LlmSettings settings = settings;

    private readonly Lock stateLock = new();
    /// <summary> Id of the last stored interaction, for server-side chaining. </summary>
    private string previousInteractionId;

    public ProtoLlmProvider Provider => ProtoLlmProvider.Gemini;

    public async Task<LlmResult> CompleteAsync(
        IReadOnlyList<LlmMessage> messages,
        int maxOutputTokens,
        CancellationToken cancellationToken) {
      try {
        string prevId;
        lock (stateLock) {
          prevId = previousInteractionId;
        }

        var url = $"{settings.BaseUrl}/v1beta/interactions";
        var body = BuildRequestBody(messages, maxOutputTokens, prevId, settings.Model);

        // The body carries no secret (the key is a header), so it is safe to log.
        // This shows the exact model id, generation_config, and whether we chained
        // via previous_interaction_id.
        Logger.Log($"{LogTag} >> POST {url}\n{body.ToJsonString()}");

        using var req = new HttpRequestMessage(HttpMethod.Post, url) {
          Content = JsonContent.Create(body),
        };
        // Key goes in a header (the only documented auth for this endpoint);
        // keep it out of the URL so it never lands in logs.
        req.Headers.TryAddWithoutValidation("x-goog-api-key", settings.ApiToken);

        using var resp = await http.SendAsync(req, cancellationToken);
        var text = await resp.Content.ReadAsStringAsync(cancellationToken);

        // Log the FULL raw response so we can see exactly what the model returned
        // (status + body), which is essential for diagnosing empty/no-text cases.
        Logger.Log($"{LogTag} << HTTP {(int)resp.StatusCode}\n{text}");

        if (!resp.IsSuccessStatusCode) {
          return LlmResult.Fail($"HTTP {(int)resp.StatusCode}: {Truncate(text)}");
        }

        var result = ParseResponse(text, out var interactionId);
        if (!result.Success) {
          Logger.Warn($"{LogTag} << parse failed ({result.Error}); id={interactionId ?? "<none>"}");
        }
        // Only advance the chain when the turn produced a usable, stored result.
        if (result.Success && !string.IsNullOrEmpty(interactionId)) {
          lock (stateLock) {
            previousInteractionId = interactionId;
          }
        }
        return result;
      } catch (OperationCanceledException) {
        return LlmResult.Fail("timeout");
      } catch (Exception e) {
        Logger.Warn($"{LogTag} << exception: {e}");
        return LlmResult.Fail(e.Message);
      }
    }

    private const string LogTag = "[llm:gemini]";

    /// <summary>
    /// Builds the interactions.create body. When <paramref name="previousInteractionId"/>
    /// is null this is a fresh conversation: the full non-system history is sent
    /// as <c>input</c> steps. Otherwise only the latest user turn is sent and the
    /// server supplies the history via <c>previous_interaction_id</c>. System
    /// instruction and generation config are interaction-scoped, so they are
    /// always (re)sent.
    /// </summary>
    public static JsonObject BuildRequestBody(
        IReadOnlyList<LlmMessage> messages, int maxOutputTokens,
        string previousInteractionId, string model) {
      var systemText = new StringBuilder();
      var nonSystem = new List<LlmMessage>();
      foreach (var m in messages) {
        if (m.Role == LlmRole.System) {
          if (systemText.Length > 0) {
            systemText.Append('\n');
          }
          systemText.Append(m.Content);
        } else {
          nonSystem.Add(m);
        }
      }

      var body = new JsonObject {
        ["model"] = model,
        ["store"] = true,
        ["generation_config"] = new JsonObject {
          ["temperature"] = 1.0,
          ["max_output_tokens"] = maxOutputTokens,
          // Flash models support "minimal", the closest available setting to
          // thinking-off. Pro models reject "minimal", so use their lowest
          // portable level, "low". This keeps the small decision JSON visible.
          ["thinking_level"] = LowestThinkingLevel(model),
        },
      };

      if (systemText.Length > 0) {
        body["system_instruction"] = systemText.ToString();
      }

      if (string.IsNullOrEmpty(previousInteractionId)) {
        // Fresh conversation: send the whole non-system history as steps.
        var steps = new JsonArray();
        foreach (var m in nonSystem) {
          steps.Add(Step(m));
        }
        body["input"] = steps;
      } else {
        // Continuing: reference stored history, send only the newest user turn.
        body["previous_interaction_id"] = previousInteractionId;
        body["input"] = LastUserText(nonSystem);
      }
      return body;
    }

    internal static string LowestThinkingLevel(string model) {
      var isGemini3 = model?.StartsWith("gemini-3", StringComparison.OrdinalIgnoreCase) == true;
      var isFlash = model?.Contains("flash", StringComparison.OrdinalIgnoreCase) == true;
      return isGemini3 && isFlash ? "minimal" : "low";
    }

    /// <summary> One Interactions "step" for a non-system message. </summary>
    private static JsonObject Step(LlmMessage m) => new() {
      ["type"] = m.Role == LlmRole.Assistant ? "model_output" : "user_input",
      ["content"] = new JsonArray {
        new JsonObject { ["type"] = "text", ["text"] = m.Content },
      },
    };

    /// <summary> Text of the last message (the fresh user turn). </summary>
    private static string LastUserText(List<LlmMessage> nonSystem) =>
        nonSystem.Count == 0 ? "" : nonSystem[^1].Content;

    /// <summary>
    /// Extracts the interaction id and the model's text from a non-streaming
    /// interactions response. Tolerant of the documented shape variants:
    /// <c>steps[].content[].text</c> (model_output steps), <c>outputs[].text</c>,
    /// and the <c>output_text</c> convenience field.
    /// </summary>
    public static LlmResult ParseResponse(string json, out string interactionId) {
      interactionId = null;
      try {
        var node = JsonNode.Parse(json);
        if (node == null) {
          return LlmResult.Fail("empty response");
        }
        // id lives at "id"; accept "name" as a fallback just in case.
        interactionId = AsString(node["id"]) ?? AsString(node["name"]);

        var text = ExtractText(node);
        if (!string.IsNullOrWhiteSpace(text)) {
          return LlmResult.Ok(text);
        }
        // No visible text. If the server still returned a well-formed
        // interaction (it has an id), the model IS reachable/valid — it just
        // spent its (tiny) budget on thinking. Signal that distinctly so a
        // connectivity check can accept it, while in-game callers still treat it
        // as a (recoverable) non-answer.
        return string.IsNullOrEmpty(interactionId)
            ? LlmResult.Fail("empty response")
            : LlmResult.Fail("no output text");
      } catch (Exception e) {
        return LlmResult.Fail($"parse error: {e.Message}");
      }
    }

    /// <summary>
    /// Pulls the assistant's text out of an interactions response, trying each
    /// documented shape in turn: model_output steps, then outputs[], then the
    /// output_text convenience field.
    /// </summary>
    private static string ExtractText(JsonNode node) {
      // 1) steps[] with type == "model_output", content[] text blocks.
      var fromSteps = TextFromContentSteps(node["steps"]?.AsArray(), "model_output");
      if (!string.IsNullOrWhiteSpace(fromSteps)) {
        return fromSteps;
      }
      // 2) outputs[] with { text } or { content:[{text}] } entries.
      var outputs = node["outputs"]?.AsArray();
      if (outputs != null) {
        var sb = new StringBuilder();
        foreach (var o in outputs) {
          var direct = AsString(o?["text"]);
          if (!string.IsNullOrEmpty(direct)) {
            sb.Append(direct);
          } else {
            AppendTextBlocks(o?["content"]?.AsArray(), sb);
          }
        }
        if (sb.Length > 0) {
          return sb.ToString();
        }
      }
      // 3) output_text convenience field.
      return AsString(node["output_text"]) ?? "";
    }

    /// <summary> Concatenates text blocks from steps of the given type. </summary>
    private static string TextFromContentSteps(JsonArray steps, string stepType) {
      if (steps == null) {
        return null;
      }
      var sb = new StringBuilder();
      foreach (var step in steps) {
        if (step?["type"]?.GetValue<string>() != stepType) {
          continue;
        }
        AppendTextBlocks(step?["content"]?.AsArray(), sb);
      }
      return sb.ToString();
    }

    /// <summary> Appends the text of every {type:"text", text} block. </summary>
    private static void AppendTextBlocks(JsonArray content, StringBuilder sb) {
      if (content == null) {
        return;
      }
      foreach (var block in content) {
        if (block?["type"]?.GetValue<string>() != "text") {
          continue;
        }
        var t = AsString(block?["text"]);
        if (!string.IsNullOrEmpty(t)) {
          sb.Append(t);
        }
      }
    }

    /// <summary> Safe string read that tolerates non-string / missing nodes. </summary>
    private static string AsString(JsonNode node) {
      try {
        return node?.GetValue<string>();
      } catch {
        return null;
      }
    }

    private static string Truncate(string s) =>
        string.IsNullOrEmpty(s) ? s : (s.Length <= 200 ? s : s[..200]);
  }
}
