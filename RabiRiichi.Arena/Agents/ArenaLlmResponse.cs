using System.Text.Json.Nodes;
using RabiRiichi.Server.Agents.Llm;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// A parsed <c>{ action, rationale }</c> answer from an Arena playing agent.
  /// The model is asked to choose a legal action by its menu id (see
  /// <see cref="ArenaPromptBuilder"/>) and to explain the move in one short
  /// line. Parsing is tolerant of Markdown fences and surrounding prose,
  /// reusing <see cref="LlmJsonParser"/> exactly as the chat path does.
  /// </summary>
  public sealed class ArenaLlmResponse {
    /// <summary>The chosen menu id, or null when the model omitted/garbled it.</summary>
    public int? ActionId { get; init; }

    /// <summary>The model's short explanation for its move, or null.</summary>
    public string Rationale { get; init; }

    /// <summary>Whether a usable <c>action</c> id was present in the response.</summary>
    public bool HasAction => ActionId.HasValue;

    /// <summary>
    /// Parses a raw model response into an <see cref="ArenaLlmResponse"/>.
    /// Finds the first JSON object that carries an <c>action</c> or
    /// <c>rationale</c> field. Never throws; a fully unparseable response
    /// yields an empty result (no action, no rationale).
    /// </summary>
    public static ArenaLlmResponse Parse(string raw) {
      try {
        var node = LlmJsonParser.FindObject(raw, IsActionObject);
        if (node == null) {
          return new ArenaLlmResponse();
        }
        return new ArenaLlmResponse {
          ActionId = ReadInt(node, "action"),
          Rationale = ReadString(node, "rationale"),
        };
      } catch {
        return new ArenaLlmResponse();
      }
    }

    private static bool IsActionObject(JsonObject node) =>
        node.ContainsKey("action") || node.ContainsKey("rationale");

    private static int? ReadInt(JsonObject node, string key) {
      if (!node.TryGetPropertyValue(key, out var v) || v == null) {
        return null;
      }
      // Accept either a JSON number or a numeric string (models vary).
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
