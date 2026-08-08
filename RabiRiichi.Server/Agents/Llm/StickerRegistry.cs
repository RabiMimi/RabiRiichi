using System.Collections.Generic;
using System.Linq;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Server-side knowledge of the stickers available on the client, so an LLM
  /// player can pick a valid sticker to express itself.
  ///
  /// This is intentionally hardcoded (mirrored from the web client's
  /// <c>src/domain/character.ts</c>). The chat wire format is
  /// <c>"{characterId}/{stickerFile}"</c>, e.g. <c>"mimi/angry.png"</c>. Keep
  /// this in sync if the client sticker set changes.
  /// </summary>
  public static class StickerRegistry {
    /// <summary> The character id whose stickers LLM players use. </summary>
    public const string CharacterId = "mimi";

    /// <summary>
    /// Sticker file -> a short mood label the LLM can reason about. The label is
    /// deliberately English/neutral; the LLM maps its intent to one of these.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Stickers =
        new Dictionary<string, string> {
          ["angry.png"] = "angry",
          ["awawawa.png"] = "panic",
          ["happy.png"] = "happy",
          ["smile.png"] = "smile",
          ["speechless.png"] = "speechless",
          ["surprised.png"] = "surprised",
        };

    /// <summary> The mood labels the LLM may choose from. </summary>
    public static IEnumerable<string> Moods => Stickers.Values;

    /// <summary>
    /// Resolves an LLM-chosen mood label to a full chat sticker path, or null
    /// if the mood is not recognized.
    /// </summary>
    public static string ResolvePath(string mood) {
      if (string.IsNullOrWhiteSpace(mood)) {
        return null;
      }
      var normalized = mood.Trim().ToLowerInvariant();
      var match = Stickers.FirstOrDefault(kv => kv.Value == normalized);
      return match.Key == null ? null : $"{CharacterId}/{match.Key}";
    }
  }
}
