using System;
using System.Text.Json.Nodes;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Extracts JSON objects from model-generated text. Models sometimes wrap a
  /// requested object in Markdown, prose, or other JSON fragments, so callers
  /// can supply a predicate describing the object they actually expect.
  /// </summary>
  public static class LlmJsonParser {
    /// <summary>
    /// Finds the first balanced, valid JSON object accepted by
    /// <paramref name="predicate"/>. Braces inside JSON strings are ignored.
    /// Invalid or unrelated objects do not prevent later candidates from being
    /// considered. Returns null when no matching object is found.
    /// </summary>
    public static JsonObject FindObject(
        string raw, Func<JsonObject, bool> predicate = null) {
      if (string.IsNullOrWhiteSpace(raw)) {
        return null;
      }

      for (int start = raw.IndexOf('{'); start >= 0;
          start = raw.IndexOf('{', start + 1)) {
        if (!TryFindObjectEnd(raw, start, out var end)) {
          continue;
        }
        try {
          if (JsonNode.Parse(raw.Substring(start, end - start + 1)) is JsonObject candidate
              && (predicate == null || predicate(candidate))) {
            return candidate;
          }
        } catch {
          // Model prose can contain brace-delimited text. Keep scanning for a
          // later valid JSON object rather than rejecting the entire response.
        }
      }
      return null;
    }

    private static bool TryFindObjectEnd(string raw, int start, out int end) {
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

        if (c == '"') {
          inString = true;
        } else if (c == '{') {
          depth++;
        } else if (c == '}' && --depth == 0) {
          end = i;
          return true;
        }
      }
      end = -1;
      return false;
    }
  }
}
