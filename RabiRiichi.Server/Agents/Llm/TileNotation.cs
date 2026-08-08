using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Compact, token-frugal tile notation for LLM prompts, matching the standard
  /// riichi shorthand: digits grouped by suit letter (m/p/s), honors as 1-7z,
  /// and red fives as 0 (e.g. 0p = red 5 pin). Honor names are attached every
  /// time to prevent the model from confusing winds, dragons, and players.
  /// </summary>
  public static class TileNotation {
    private static readonly string[] EnglishHonorNames = [
      "East wind", "South wind", "West wind", "North wind",
      "White dragon", "Green dragon", "Red dragon",
    ];
    private static readonly string[] JapaneseHonorNames = [
      "東・風牌", "南・風牌", "西・風牌", "北・風牌",
      "白・三元牌", "發・三元牌", "中・三元牌",
    ];
    private static readonly string[] ChineseHonorNames = [
      "东风牌", "南风牌", "西风牌", "北风牌",
      "白，三元牌", "发，三元牌", "中，三元牌",
    ];

    /// <summary> Suit letter for a tile. </summary>
    private static char SuitChar(TileSuit suit) => suit switch {
      TileSuit.M => 'm',
      TileSuit.P => 'p',
      TileSuit.S => 's',
      TileSuit.Z => 'z',
      _ => '?',
    };

    /// <summary> The digit for a single tile (0 for a red five). </summary>
    private static char Digit(Tile t) => (t.Akadora && t.Num == 5) ? '0' : (char)('0' + t.Num);

    private static string HonorName(int number, string language) {
      var names = AiLocalization.NormalizeLanguage(language) switch {
        AiLocalization.LangJa => JapaneseHonorNames,
        AiLocalization.LangZhs => ChineseHonorNames,
        _ => EnglishHonorNames,
      };
      return number is >= 1 and <= 7 ? names[number - 1] : "unknown honor";
    }

    /// <summary> A single tile, e.g. "5p", "0p" (red), or "1z(East wind)". </summary>
    public static string One(Tile t, string language = AiLocalization.LangEn) {
      if (!t.IsValid) {
        return "?";
      }
      var notation = $"{Digit(t)}{SuitChar(t.Suit)}";
      return t.Suit == TileSuit.Z
          ? $"{notation}({HonorName(t.Num, language)})"
          : notation;
    }

    public static string One(
        GameTile t, string language = AiLocalization.LangEn) => One(t.tile, language);

    /// <summary>
    /// A sorted, suit-grouped notation for a set of tiles. Numbered suits stay
    /// compact, while every honor is expanded, e.g.
    /// "123m456p789s 1z(East wind) 1z(East wind)".
    /// </summary>
    public static string Group(
        IEnumerable<Tile> tiles, string language = AiLocalization.LangEn) {
      var list = tiles.Where(t => t.IsValid).ToList();
      if (list.Count == 0) {
        return "";
      }
      var sb = new StringBuilder();
      foreach (var suit in new[] { TileSuit.M, TileSuit.P, TileSuit.S }) {
        var inSuit = list.Where(t => t.Suit == suit)
            .OrderBy(t => t.Num).ThenByDescending(t => t.Akadora).ToList();
        if (inSuit.Count == 0) {
          continue;
        }
        foreach (var t in inSuit) {
          sb.Append(Digit(t));
        }
        sb.Append(SuitChar(suit));
      }
      var honors = list.Where(t => t.Suit == TileSuit.Z).OrderBy(t => t.Num).ToList();
      if (honors.Count > 0) {
        if (sb.Length > 0) sb.Append(' ');
        sb.Append(string.Join(" ", honors.Select(tile => One(tile, language))));
      }
      return sb.ToString();
    }

    public static string Group(
        IEnumerable<GameTile> tiles, string language = AiLocalization.LangEn) =>
        Group(tiles.Select(t => t.tile), language);

    /// <summary> A called meld, marking the claimed tile with a leading '-'. </summary>
    public static string Meld(
        MenLike meld, string language = AiLocalization.LangEn) {
      // Represent as the group; prefix with '-' if it is an open (claimed) meld.
      var open = meld.Any(t => !t.IsTsumo);
      var body = Group(meld.Select(t => t.tile), language);
      return open ? "-" + body : body;
    }
  }
}
