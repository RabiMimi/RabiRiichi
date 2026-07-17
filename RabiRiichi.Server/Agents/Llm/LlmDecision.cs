using System;
using System.Text.Json.Nodes;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary> A parsed LLM decision: which choice, plus optional chat/sticker. </summary>
  public sealed class LlmDecision {
    /// <summary> The chosen menu id, or -1 if none/invalid. </summary>
    public int Choice { get; init; } = -1;
    /// <summary> Optional chat message to broadcast, or null. </summary>
    public string Say { get; init; }
    /// <summary> Optional sticker mood label, or null. </summary>
    public string Sticker { get; init; }

    public bool HasChoice => Choice >= 0;

    /// <summary>
    /// Parses an LLM response into a decision. Tolerant of code fences and of
    /// leading/trailing prose: it extracts the first balanced JSON object.
    /// Returns a decision with Choice = -1 if no valid object/choice is found.
    /// </summary>
    public static LlmDecision Parse(string raw) {
      var json = ExtractJsonObject(raw);
      if (json == null) {
        return new LlmDecision();
      }
      try {
        var node = JsonNode.Parse(json)?.AsObject();
        if (node == null) {
          return new LlmDecision();
        }
        return new LlmDecision {
          Choice = ReadInt(node, "choice"),
          Say = ReadString(node, "say"),
          Sticker = ReadString(node, "sticker"),
        };
      } catch {
        return new LlmDecision();
      }
    }

    private static int ReadInt(JsonObject node, string key) {
      if (!node.TryGetPropertyValue(key, out var v) || v == null) {
        return -1;
      }
      try {
        // Accept both number and numeric-string.
        if (v is JsonValue jv) {
          if (jv.TryGetValue(out int i)) {
            return i;
          }
          if (jv.TryGetValue(out string s) && int.TryParse(s, out var si)) {
            return si;
          }
        }
      } catch {
        // fall through
      }
      return -1;
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

    /// <summary>
    /// Extracts the first balanced {...} object from arbitrary text (strips code
    /// fences and surrounding prose). Returns null if none found.
    /// </summary>
    public static string ExtractJsonObject(string raw) {
      if (string.IsNullOrWhiteSpace(raw)) {
        return null;
      }
      int start = raw.IndexOf('{');
      if (start < 0) {
        return null;
      }
      int depth = 0;
      bool inString = false;
      bool escape = false;
      for (int i = start; i < raw.Length; i++) {
        char c = raw[i];
        if (inString) {
          if (escape) {
            escape = false;
          } else if (c == '\\') {
            escape = true;
          } else if (c == '"') {
            inString = false;
          }
          continue;
        }
        switch (c) {
          case '"':
            inString = true;
            break;
          case '{':
            depth++;
            break;
          case '}':
            depth--;
            if (depth == 0) {
              return raw.Substring(start, i - start + 1);
            }
            break;
        }
      }
      return null;
    }
  }
}
