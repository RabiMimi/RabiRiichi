using System;
using System.Text.Json.Nodes;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// A parsed turn response from the LLM: which legal action it chose, plus an
  /// optional chat message and sticker.
  ///
  /// <see cref="Action"/> is the id of an entry in the turn's
  /// <see cref="LlmActionMenu"/>. It is nullable because a model may omit or
  /// garble it; the caller then falls back to the rule-based move rather than
  /// letting a malformed answer decide the game.
  /// </summary>
  public sealed class LlmDecision {
    /// <summary> Chosen menu id, or null when the model did not pick one. </summary>
    public int? Action { get; init; }

    /// <summary> Optional chat message to broadcast, or null. </summary>
    public string Say { get; init; }

    /// <summary> Optional sticker mood label, or null. </summary>
    public string Sticker { get; init; }

    /// <summary> Whether a usable action id was present in the response. </summary>
    public bool HasAction => Action.HasValue;

    /// <summary>
    /// Parses an LLM response into a decision. Tolerant of code fences and of
    /// leading/trailing prose: it finds the first valid decision JSON object.
    /// Returns an empty response if no valid object is found.
    /// </summary>
    public static LlmDecision Parse(string raw) {
      try {
        var node = LlmJsonParser.FindObject(raw, IsDecisionObject);
        if (node == null) {
          return new LlmDecision();
        }
        return new LlmDecision {
          Action = ReadInt(node, "action"),
          Say = ReadString(node, "say"),
          Sticker = ReadString(node, "sticker"),
        };
      } catch {
        return new LlmDecision();
      }
    }

    private static bool IsDecisionObject(JsonObject node) =>
        node.ContainsKey("action") || node.ContainsKey("say") ||
        node.ContainsKey("sticker");

    private static int? ReadInt(JsonObject node, string key) {
      if (!node.TryGetPropertyValue(key, out var v) || v == null) {
        return null;
      }
      // Models emit either a JSON number or a numeric string; accept both.
      try {
        return v.GetValue<int>();
      } catch {
        // fall through to string handling
      }
      try {
        var s = v.GetValue<string>();
        return int.TryParse(s?.Trim(), out var parsed) ? parsed : null;
      } catch {
        return null;
      }
    }

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
