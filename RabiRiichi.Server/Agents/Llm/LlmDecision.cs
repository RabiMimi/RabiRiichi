using System;
using System.Text.Json.Nodes;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>A parsed optional chat/sticker response from the LLM.</summary>
  public sealed class LlmDecision {
    /// <summary> Optional chat message to broadcast, or null. </summary>
    public string Say { get; init; }
    /// <summary> Optional sticker mood label, or null. </summary>
    public string Sticker { get; init; }

    /// <summary>
    /// Parses an LLM response into a decision. Tolerant of code fences and of
    /// leading/trailing prose: it finds the first valid decision JSON object.
    /// Returns an empty response if no valid chat object is found.
    /// </summary>
    public static LlmDecision Parse(string raw) {
      try {
        var node = LlmJsonParser.FindObject(raw, IsDecisionObject);
        if (node == null) {
          return new LlmDecision();
        }
        return new LlmDecision {
          Say = ReadString(node, "say"),
          Sticker = ReadString(node, "sticker"),
        };
      } catch {
        return new LlmDecision();
      }
    }

    private static bool IsDecisionObject(JsonObject node) =>
        node.ContainsKey("say") || node.ContainsKey("sticker");

    private static string ReadString(JsonObject node, string key) {
      if (!node.TryGetPropertyValue(key, out var v) || v == null) {
        return null;
      }
      try {
        var s = v.GetValue<string>();
        return string.IsNullOrWhiteSpace(s) ? null : s.Trim();
      } catch {
        return null;
      }
    }

  }
}
