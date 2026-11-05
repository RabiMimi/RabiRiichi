using System.Text.Json.Nodes;
using RabiRiichi.Server.Agents.Llm;

namespace RabiRiichi.Arena.Agents {
  /// <summary>
  /// Parsed JSON decision (<c>{ action, rationale, chat? }</c>) from an Arena LLM agent.
  /// Tolerant of Markdown code fences and surrounding prose via <see cref="LlmJsonParser"/>.
  /// </summary>
  public sealed class ArenaLlmResponse {
    public int? ActionId { get; init; }
    public string Rationale { get; init; }
    public string Chat { get; init; }

    public bool HasAction => ActionId.HasValue;

    public static ArenaLlmResponse Parse(string raw) {
      try {
        var node = LlmJsonParser.FindObject(raw, IsActionObject);
        if (node == null) {
          return new ArenaLlmResponse();
        }
        var chat = ReadString(node, "chat");
        if (chat is { Length: > 200 }) {
          chat = chat[..200];
        }
        return new ArenaLlmResponse {
          ActionId = ReadInt(node, "action"),
          Rationale = ReadString(node, "rationale"),
          Chat = chat,
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
      try {
        return v.GetValue<int>();
      } catch {
        // Fall through to string handling.
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
