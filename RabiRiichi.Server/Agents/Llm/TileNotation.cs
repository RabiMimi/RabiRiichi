using System.Collections.Generic;
using System.Linq;
using System.Text;
using RabiRiichi.Core;
using RabiRiichi.Generated.Core;

namespace RabiRiichi.Server.Agents.Llm {
  /// <summary>
  /// Compact, token-frugal tile notation for LLM prompts, matching the standard
  /// riichi shorthand: digits grouped by suit letter (m/p/s), honors as 1-7z
  /// (E S W N + White Green Red), red fives as 0 (e.g. 0p = red 5 pin).
  /// A whole hand collapses like "123m456p789s11z".
  /// </summary>
  public static class TileNotation {
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

    /// <summary> A single tile, e.g. "5p" or "0p" (red) or "1z". </summary>
    public static string One(Tile t) {
      if (!t.IsValid) {
        return "?";
      }
      return $"{Digit(t)}{SuitChar(t.Suit)}";
    }

    public static string One(GameTile t) => One(t.tile);

    /// <summary>
    /// A sorted, suit-grouped notation for a set of tiles, e.g.
    /// "123m456p789s11z". Tiles are sorted by suit then number; red fives keep
    /// their 0 digit but sort as 5.
    /// </summary>
    public static string Group(IEnumerable<Tile> tiles) {
      var list = tiles.Where(t => t.IsValid).ToList();
      if (list.Count == 0) {
        return "";
      }
      var sb = new StringBuilder();
      foreach (var suit in new[] { TileSuit.M, TileSuit.P, TileSuit.S, TileSuit.Z }) {
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
      return sb.ToString();
    }

    public static string Group(IEnumerable<GameTile> tiles) => Group(tiles.Select(t => t.tile));

    /// <summary> A called meld, marking the claimed tile with a leading '-'. </summary>
    public static string Meld(MenLike meld) {
      // Represent as the group; prefix with '-' if it is an open (claimed) meld.
      var open = meld.Any(t => !t.IsTsumo);
      var body = Group(meld.Select(t => t.tile));
      return open ? "-" + body : body;
    }
  }
}
